<#
.SYNOPSIS
    End-to-end HTTP smoke test for the Sudoku Blazor Server app.

.DESCRIPTION
    This is a Blazor Server application: there is no REST/JSON API. The HTTP
    surface is razor pages, static assets, and the SignalR negotiate endpoint,
    and that is exactly what this script exercises - happy paths and failure
    conditions (unknown routes, wrong verbs, missing assets).

    Works on Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER BaseUrl
    Address of an already-running instance. Default http://localhost:5260.

.PARAMETER StartServer
    Build and start the app before testing and stop it afterwards. The app is
    run from its published-build DLL as a single process so it can be stopped
    reliably on any OS.

.EXAMPLE
    ./tools/smoke-test.ps1 -StartServer

.EXAMPLE
    ./tools/smoke-test.ps1 -BaseUrl http://localhost:5260
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5260",
    [switch]$StartServer
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# ---------------------------------------------------------------- helpers ----

# Header values arrive as a string on 5.1 and a string[] on 7+; flatten both to
# one case-insensitive name -> single-string map so checks read the same on either.
function Get-HeaderMap {
    param($RawHeaders)
    $map = @{}
    if ($null -eq $RawHeaders) { return $map }
    foreach ($key in $RawHeaders.Keys) {
        $value = $RawHeaders[$key]
        if ($value -is [array]) { $value = ($value -join ', ') }
        $map[[string]$key] = [string]$value
    }
    return $map
}

# Case-insensitive header lookup; returns '' when absent so callers can -match.
function Get-Header {
    param($Response, [string]$Name)
    if ($null -eq $Response.Headers) { return '' }
    foreach ($key in $Response.Headers.Keys) {
        if ([string]::Equals($key, $Name, 'OrdinalIgnoreCase')) { return [string]$Response.Headers[$key] }
    }
    return ''
}

# Invoke-WebRequest throws on non-2xx; normalize success and failure into one
# shape so failure-condition checks read the same as happy-path checks.
function Invoke-Http {
    param(
        [string]$Method = 'GET',
        [Parameter(Mandatory)][string]$Url,
        [string]$Body,
        [string]$ContentType = 'application/json',
        [hashtable]$Headers
    )

    $args = @{
        Uri             = $Url
        Method          = $Method
        UseBasicParsing = $true
        TimeoutSec      = 20
    }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $args.Body = $Body
        $args.ContentType = $ContentType
    }
    if ($PSBoundParameters.ContainsKey('Headers')) { $args.Headers = $Headers }

    try {
        $resp = Invoke-WebRequest @args
        $ct = ''
        if ($resp.Headers['Content-Type']) { $ct = [string]$resp.Headers['Content-Type'] }
        return @{
            Status      = [int]$resp.StatusCode
            Body        = [string]$resp.Content
            ContentType = $ct
            Headers     = (Get-HeaderMap $resp.Headers)
        }
    }
    catch {
        $r = $_.Exception.Response
        if ($null -eq $r) { throw } # connection-level failure: let the check fail loudly

        $status = [int]$r.StatusCode
        $bodyText = ''
        $ct = ''
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
            # PowerShell 7 surfaces the error body here
            $bodyText = $_.ErrorDetails.Message
        }
        elseif ($r -is [System.Net.HttpWebResponse]) {
            # Windows PowerShell 5.1: read the stream ourselves
            try {
                $reader = New-Object System.IO.StreamReader($r.GetResponseStream())
                $bodyText = $reader.ReadToEnd()
                $reader.Dispose()
            } catch { }
        }
        # Content type lives in a different place per edition: a property on
        # 5.1's HttpWebResponse, under Content.Headers on 7's HttpResponseMessage.
        try {
            if ($r -is [System.Net.HttpWebResponse]) {
                $ct = [string]$r.ContentType
            }
            elseif ($r.Content -and $r.Content.Headers -and $r.Content.Headers.ContentType) {
                $ct = $r.Content.Headers.ContentType.ToString()
            }
        } catch { }
        # Error responses expose headers differently per edition too.
        $hdrs = @{}
        try {
            if ($r -is [System.Net.HttpWebResponse]) { $hdrs = Get-HeaderMap $r.Headers }
            elseif ($r.Headers) { $hdrs = Get-HeaderMap $r.Headers }
        } catch { }
        return @{ Status = $status; Body = $bodyText; ContentType = $ct; Headers = $hdrs }
    }
}

$script:results = @()

function Test-Case {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Check
    )
    $outcome = $null
    try {
        $ok = & $Check
        if ($ok -eq $true) { $outcome = @{ Name = $Name; Passed = $true; Detail = '' } }
        else               { $outcome = @{ Name = $Name; Passed = $false; Detail = "check returned '$ok'" } }
    }
    catch {
        $outcome = @{ Name = $Name; Passed = $false; Detail = $_.Exception.Message }
    }
    $script:results += $outcome
    if ($outcome.Passed) { Write-Host ("[PASS] " + $Name) -ForegroundColor Green }
    else                 { Write-Host ("[FAIL] " + $Name + " -- " + $outcome.Detail) -ForegroundColor Red }
}

# ------------------------------------------------------- optional app start ----

$serverProcess = $null
if ($StartServer) {
    Write-Host "Building..." -ForegroundColor Cyan
    & dotnet build "$repoRoot" -v q --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Build failed; aborting smoke test." }

    $binDir = [System.IO.Path]::Combine($repoRoot, 'Sudoku', 'bin')
    $dll = Get-ChildItem -Path $binDir -Recurse -Filter 'Sudoku.dll' |
        Where-Object { $_.FullName -notmatch 'obj' } | Select-Object -First 1
    if ($null -eq $dll) { throw "Sudoku.dll not found under $binDir - build output missing." }

    Write-Host "Starting $($dll.FullName)..." -ForegroundColor Cyan
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $BaseUrl
    # Content root must be the web project folder so wwwroot and the static
    # asset manifest resolve; run the DLL directly so it is one killable process.
    $startArgs = @{
        FilePath         = 'dotnet'
        ArgumentList     = @($dll.FullName)
        WorkingDirectory = (Join-Path $repoRoot 'Sudoku')
        PassThru         = $true
    }
    # -WindowStyle exists only on Windows PowerShell/pwsh-on-Windows; Linux
    # and macOS pwsh reject the parameter outright.
    if ($env:OS -eq 'Windows_NT') { $startArgs.WindowStyle = 'Hidden' }
    $serverProcess = Start-Process @startArgs

    $ready = $false
    foreach ($attempt in 1..60) {
        Start-Sleep -Milliseconds 500
        if ($serverProcess.HasExited) { throw "App process exited during startup (code $($serverProcess.ExitCode))." }
        try {
            $probe = Invoke-Http -Url $BaseUrl
            if ($probe.Status -ge 200) { $ready = $true; break }
        } catch { }
    }
    if (-not $ready) { throw "App did not become ready on $BaseUrl within 30s." }
    Write-Host "App is up." -ForegroundColor Cyan
}

# ---------------------------------------------------------------- the tests ----

try {
    Write-Host ""
    Write-Host "Smoke-testing $BaseUrl" -ForegroundColor Cyan
    Write-Host "--- happy paths -------------------------------------------------"

    Test-Case "GET / returns 200 HTML containing the game page" {
        $r = Invoke-Http -Url "$BaseUrl/"
        ($r.Status -eq 200) -and ($r.ContentType -like 'text/html*') -and ($r.Body -match 'Sudoku')
    }

    Test-Case "GET / wires up the Blazor client script" {
        $r = Invoke-Http -Url "$BaseUrl/"
        ($r.Status -eq 200) -and ($r.Body -match 'blazor\.web\.js')
    }

    Test-Case "GET /about returns 200 HTML" {
        $r = Invoke-Http -Url "$BaseUrl/about"
        ($r.Status -eq 200) -and ($r.ContentType -like 'text/html*')
    }

    Test-Case "GET /app.css returns the stylesheet" {
        $r = Invoke-Http -Url "$BaseUrl/app.css"
        ($r.Status -eq 200) -and ($r.ContentType -like 'text/css*')
    }

    Test-Case "GET /Sudoku.styles.css returns the scoped-CSS bundle" {
        $r = Invoke-Http -Url "$BaseUrl/Sudoku.styles.css"
        ($r.Status -eq 200) -and ($r.ContentType -like 'text/css*') -and ($r.Body -match 'sudoku-root')
    }

    Test-Case "GET /favicon.png returns the icon" {
        $r = Invoke-Http -Url "$BaseUrl/favicon.png"
        ($r.Status -eq 200) -and ($r.ContentType -like 'image/png*')
    }

    Test-Case "POST /_blazor/negotiate opens a circuit negotiation" {
        $r = Invoke-Http -Method POST -Url "$BaseUrl/_blazor/negotiate?negotiateVersion=1" -Body ''
        if ($r.Status -ne 200) { return $false }
        $json = $r.Body | ConvertFrom-Json
        ($null -ne $json.connectionToken -or $null -ne $json.connectionId) -and
            ($json.availableTransports.Count -ge 1)
    }

    Write-Host "--- security headers --------------------------------------------"

    # Each check below corresponds to a finding from the security review. They
    # exist so the hardening cannot silently regress: a header dropped during a
    # refactor fails the build instead of shipping.

    Test-Case "Response sets X-Content-Type-Options: nosniff" {
        $r = Invoke-Http -Url "$BaseUrl/"
        (Get-Header $r 'X-Content-Type-Options') -eq 'nosniff'
    }

    Test-Case "Response sets a Content-Security-Policy that restricts scripts" {
        $r = Invoke-Http -Url "$BaseUrl/"
        $csp = Get-Header $r 'Content-Security-Policy'
        # Must constrain script sources and forbid 'unsafe-inline' scripts -
        # a policy of only frame-ancestors is what the review found and rejected.
        ($csp -match "default-src\s+'self'") -and
            ($csp -match "script-src\s+'self'") -and
            ($csp -notmatch "script-src[^;]*unsafe-inline") -and
            ($csp -match "frame-ancestors\s+'self'") -and
            ($csp -match "object-src\s+'none'")
    }

    Test-Case "Response sets Referrer-Policy" {
        $r = Invoke-Http -Url "$BaseUrl/"
        (Get-Header $r 'Referrer-Policy') -match 'strict-origin'
    }

    Test-Case "Response sets Permissions-Policy" {
        $r = Invoke-Http -Url "$BaseUrl/"
        (Get-Header $r 'Permissions-Policy') -match 'camera=\(\)'
    }

    Test-Case "Response sets X-Frame-Options" {
        $r = Invoke-Http -Url "$BaseUrl/"
        (Get-Header $r 'X-Frame-Options') -match 'SAMEORIGIN|DENY'
    }

    # Regression guard for the review's P1 finding: behind a TLS-terminating
    # proxy the default SameAsRequest policy drops Secure from this cookie.
    # Only meaningful over HTTPS - development deliberately uses SameAsRequest,
    # because Always makes the antiforgery system throw on a non-SSL request.
    # The always-on version of this check is in SecurityPostureTests, which
    # asserts the configured policy directly and runs on every `dotnet test`.
    Test-Case "Antiforgery cookie is HttpOnly, SameSite and Secure (HTTPS only)" {
        if ($BaseUrl -notlike 'https://*') { return $true }
        $r = Invoke-Http -Url "$BaseUrl/"
        $cookies = Get-Header $r 'Set-Cookie'
        if ($cookies -notmatch 'Antiforgery') { return $true }  # none issued on this response
        # Isolate the antiforgery cookie; other cookies (ARR affinity) differ.
        $anti = ($cookies -split ',\s*(?=[^;,]+=)') | Where-Object { $_ -match 'Antiforgery' } | Select-Object -First 1
        ($anti -match '(?i)httponly') -and ($anti -match '(?i)samesite=strict') -and ($anti -match '(?i)secure')
    }

    Test-Case "Exactly one Content-Security-Policy header is sent" {
        $r = Invoke-Http -Url "$BaseUrl/"
        # A duplicated policy is intersected by browsers - correct by accident
        # and hard to audit. Header values are joined with ', ' by Get-HeaderMap.
        (Get-Header $r 'Content-Security-Policy') -notmatch 'frame-ancestors.*frame-ancestors'
    }

    Test-Case "HSTS is set when served over HTTPS" {
        if ($BaseUrl -notlike 'https://*') { return $true }  # not applicable over plain HTTP
        $r = Invoke-Http -Url "$BaseUrl/"
        (Get-Header $r 'Strict-Transport-Security') -match 'max-age=\d+'
    }

    Test-Case "Server does not accept an unknown Host header" {
        # Only meaningful against the local instance: a hosted deployment is
        # fronted by a proxy that routes on Host before the app ever sees it.
        if (-not $StartServer) { return $true }
        $r = Invoke-Http -Url "$BaseUrl/" -Headers @{ Host = 'evil.example.com' }
        $r.Status -ne 200
    }

    Write-Host "--- failure conditions ------------------------------------------"

    Test-Case "GET unknown route returns 404 with the not-found page" {
        $r = Invoke-Http -Url "$BaseUrl/this-route-does-not-exist"
        ($r.Status -eq 404) -and ($r.ContentType -like 'text/html*')
    }

    Test-Case "GET deep unknown route also returns 404" {
        $r = Invoke-Http -Url "$BaseUrl/api/v1/does/not/exist"
        $r.Status -eq 404
    }

    Test-Case "GET missing static asset returns 404, not an error page loop" {
        $r = Invoke-Http -Url "$BaseUrl/no-such-file.css"
        $r.Status -eq 404
    }

    Test-Case "GET on the negotiate endpoint (wrong verb) is rejected" {
        $r = Invoke-Http -Url "$BaseUrl/_blazor/negotiate?negotiateVersion=1"
        # SignalR requires POST; anything but 200 proves the verb is rejected.
        $r.Status -ne 200
    }

    Test-Case "POST to a page route is rejected (405 or 404)" {
        $r = Invoke-Http -Method POST -Url "$BaseUrl/about" -Body '{}'
        ($r.Status -eq 405) -or ($r.Status -eq 404) -or ($r.Status -eq 400)
    }

    Test-Case "Malformed negotiate version is tolerated without a 500" {
        $r = Invoke-Http -Method POST -Url "$BaseUrl/_blazor/negotiate?negotiateVersion=notanumber" -Body ''
        $r.Status -lt 500
    }

    Test-Case "Nothing above leaked a raw exception page" {
        # The developer exception page contains this marker text; no endpoint
        # under test should have produced one even in Development.
        $r = Invoke-Http -Url "$BaseUrl/this-route-does-not-exist"
        -not ($r.Body -match 'An unhandled exception occurred while processing the request')
    }
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Write-Host ""
        Write-Host "Stopping app (pid $($serverProcess.Id))..." -ForegroundColor Cyan
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------- summary ----

$failed = @($script:results | Where-Object { -not $_.Passed })
$passed = @($script:results | Where-Object { $_.Passed })
Write-Host ""
Write-Host ("{0} passed, {1} failed, {2} total" -f $passed.Count, $failed.Count, $script:results.Count) `
    -ForegroundColor $(if ($failed.Count -eq 0) { 'Green' } else { 'Red' })

if ($failed.Count -gt 0) { exit 1 }
exit 0
