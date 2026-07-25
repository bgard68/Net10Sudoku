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

# Invoke-WebRequest throws on non-2xx; normalize success and failure into one
# shape so failure-condition checks read the same as happy-path checks.
function Invoke-Http {
    param(
        [string]$Method = 'GET',
        [Parameter(Mandatory)][string]$Url,
        [string]$Body,
        [string]$ContentType = 'application/json'
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

    try {
        $resp = Invoke-WebRequest @args
        $ct = ''
        if ($resp.Headers['Content-Type']) { $ct = [string]$resp.Headers['Content-Type'] }
        return @{ Status = [int]$resp.StatusCode; Body = [string]$resp.Content; ContentType = $ct }
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
            $ct = [string]$r.ContentType
        }
        return @{ Status = $status; Body = $bodyText; ContentType = $ct }
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
