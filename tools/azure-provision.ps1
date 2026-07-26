#Requires -Version 5.1
<#
.SYNOPSIS
  Provisions every Azure Free (F1) resource and the OIDC deploy identity for
  Net10Sudoku, then wires the GitHub repo so .github/workflows/deploy.yml works.

.DESCRIPTION
  Idempotent - safe to re-run. Creates (or reuses if already present):
    1. Resource group
    2. Free F1 Linux App Service plan
    3. Web app on the .NET 10 runtime (WebSockets on, HTTPS-only, App Settings)
    4. Microsoft Entra app registration + service principal (NO client secret),
       a federated credential trusting this repo's 'production' environment,
       and a Contributor role assignment scoped to the resource group
    5. GitHub environment, the three ID secrets, and the AZURE_WEBAPP_NAME var
    6. Secret-scanning push protection

  Authentication is OIDC end to end. No publish profile and no long-lived
  credential are ever created or stored. The only values handed to GitHub are
  three non-sensitive identifiers (client / tenant / subscription IDs).

.PREREQUISITES
  - Azure CLI (az) logged in:   az login
  - GitHub CLI (gh) logged in:  gh auth login        (unless -SkipGitHub)
  - Owner or User Access Administrator on the subscription, needed once for
    the role assignment in step 4.

.EXAMPLE
  ./tools/azure-provision.ps1 -AppName net10sudoku-bgard

.EXAMPLE
  ./tools/azure-provision.ps1 -AppName net10sudoku-bgard -Location westus2 -SkipGitHub
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, HelpMessage = 'Globally-unique name -> https://<name>.azurewebsites.net')]
    [ValidatePattern('^[a-z0-9][a-z0-9-]{1,58}[a-z0-9]$')]
    [string]$AppName,

    [string]$ResourceGroup = 'rg-net10sudoku',
    [string]$Location      = 'eastus',
    [string]$PlanName      = 'plan-net10sudoku-free',
    [string]$GitHubRepo    = 'bgard68/Net10Sudoku',
    [string]$SubscriptionId,

    [switch]$SkipGitHub,
    [switch]$SkipPushProtection
)

# az writes warnings/errors to stderr and returns non-zero for expected cases
# (e.g. "resource not found" while probing). Neither should abort the script;
# we gate every real failure on $LASTEXITCODE ourselves via Assert-LastExit.
# ('Continue' + this native pref = no auto-throw; explicit `throw` still stops.
#  Assigning the native pref is harmless on PowerShell 5.1 - no such variable.)
$ErrorActionPreference = 'Continue'
$PSNativeCommandUseErrorActionPreference = $false

function Assert-LastExit([string]$What) {
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)." }
}

function Test-Cli([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' is not on PATH. Install it and try again."
    }
}

# --------------------------------------------------------------------------
Write-Host '==> Preflight' -ForegroundColor Cyan
Test-Cli az
if (-not $SkipGitHub) { Test-Cli gh }

az account show -o none 2>$null
if ($LASTEXITCODE -ne 0) { throw "Azure CLI is not logged in. Run 'az login' first." }

if ($SubscriptionId) {
    az account set --subscription $SubscriptionId
    Assert-LastExit 'az account set'
}

$SubId    = az account show --query id       -o tsv; Assert-LastExit 'read subscription id'
$TenantId = az account show --query tenantId -o tsv; Assert-LastExit 'read tenant id'
$SubName  = az account show --query name     -o tsv
Write-Host "    Subscription: $SubName ($SubId)"

if (-not $SkipGitHub) {
    gh auth status 2>$null
    if ($LASTEXITCODE -ne 0) { throw "GitHub CLI is not logged in. Run 'gh auth login', or pass -SkipGitHub." }
}

# --------------------------------------------------------------------------
Write-Host "==> 1/6 Resource group '$ResourceGroup' ($Location)" -ForegroundColor Cyan
# Reuse an existing group as-is. A group's location cannot change, so calling
# 'az group create' with a different -Location than an existing group errors;
# check first and only create when it is genuinely missing.
if ((az group exists --name $ResourceGroup) -eq 'true') {
    Write-Host '    already exists, reusing (its current location is kept)'
} else {
    az group create --name $ResourceGroup --location $Location -o none
    Assert-LastExit 'create resource group'
}

# --------------------------------------------------------------------------
Write-Host "==> 2/6 Free F1 Linux plan '$PlanName'" -ForegroundColor Cyan
az appservice plan create --name $PlanName --resource-group $ResourceGroup `
    --sku F1 --is-linux -o none
Assert-LastExit 'create app service plan'

# --------------------------------------------------------------------------
Write-Host "==> 3/6 Web app '$AppName'" -ForegroundColor Cyan
# Resolve the live .NET 10 Linux runtime token instead of hard-coding it.
$runtime = az webapp list-runtimes --os linux `
    --query "[?starts_with(@, 'DOTNETCORE:10')] | [0]" -o tsv
if (-not $runtime) { $runtime = 'DOTNETCORE:10.0' }
Write-Host "    runtime: $runtime"

# 'list' not 'show': show errors when the app is absent (the normal first-run
# case); list returns an empty string, matching the other existence checks below.
$existing = az webapp list --resource-group $ResourceGroup --query "[?name=='$AppName'] | [0].id" -o tsv
if (-not $existing) {
    az webapp create --name $AppName --resource-group $ResourceGroup --plan $PlanName `
        --runtime $runtime -o none
    Assert-LastExit 'create web app (is the name globally unique?)'
    Write-Host '    created'
} else {
    Write-Host '    already exists, reusing'
}

Write-Host '    enabling WebSockets + HTTPS-only'
az webapp config set --name $AppName --resource-group $ResourceGroup --web-sockets-enabled true -o none
Assert-LastExit 'enable web sockets'
az webapp update --name $AppName --resource-group $ResourceGroup --https-only true -o none
Assert-LastExit 'set https-only'

Write-Host '    applying App Settings (ASPNETCORE_ENVIRONMENT, AllowedHosts, no in-place build)'
# SCM_DO_BUILD_DURING_DEPLOYMENT=false is the important one: we deploy a
# pre-built 'dotnet publish' package, so Azure/Oryx must NOT try to rebuild it
# in /home/site/wwwroot. Leaving it on is the classic cause of a half-populated
# web root that boot-loops or 500s.
az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings `
    "ASPNETCORE_ENVIRONMENT=Production" `
    "AllowedHosts=$AppName.azurewebsites.net" `
    "SCM_DO_BUILD_DURING_DEPLOYMENT=false" -o none
Assert-LastExit 'set app settings'

Write-Host '    pinning the startup command (dotnet Sudoku.dll)'
# The published entry assembly is Sudoku.dll (from Sudoku.csproj) at the web
# root. App Service usually auto-detects it, but pinning it removes any doubt
# about what actually starts.
az webapp config set --name $AppName --resource-group $ResourceGroup `
    --startup-file "dotnet Sudoku.dll" -o none
Assert-LastExit 'set startup command'

# --------------------------------------------------------------------------
Write-Host '==> 4/6 Entra deploy identity (OIDC, no client secret)' -ForegroundColor Cyan
$displayName = "gh-$($GitHubRepo.Replace('/','-'))-deploy"

$AppId = az ad app list --display-name $displayName --query "[0].appId" -o tsv
if (-not $AppId) {
    $AppId = az ad app create --display-name $displayName --query appId -o tsv
    Assert-LastExit 'create app registration'
    Write-Host "    created app registration '$displayName'"
} else {
    Write-Host "    reusing app registration '$displayName'"
}

$spId = az ad sp list --filter "appId eq '$AppId'" --query "[0].id" -o tsv
if (-not $spId) {
    az ad sp create --id $AppId -o none
    Assert-LastExit 'create service principal'
}

# Federated credential. The subject MUST match how deploy.yml runs: the deploy
# job pins 'environment: production', so the subject is the environment form
# below - NOT a branch ref. This is the most common setup mistake.
$fcName    = 'gh-net10sudoku-production'
$fcSubject = "repo:${GitHubRepo}:environment:production"
$fcExists  = az ad app federated-credential list --id $AppId --query "[?name=='$fcName'] | [0].name" -o tsv
if (-not $fcExists) {
    $fcJson = @{
        name      = $fcName
        issuer    = 'https://token.actions.githubusercontent.com'
        subject   = $fcSubject
        audiences = @('api://AzureADTokenExchange')
    } | ConvertTo-Json -Compress
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, $fcJson)   # UTF-8, no BOM
    az ad app federated-credential create --id $AppId --parameters "@$tmp" -o none
    $rc = $LASTEXITCODE
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    if ($rc -ne 0) { throw "create federated credential failed (exit $rc)." }
    Write-Host "    federated credential -> $fcSubject"
} else {
    Write-Host '    federated credential already present'
}

# Contributor, scoped to the resource group only (least privilege for deploy).
$scope = "/subscriptions/$SubId/resourceGroups/$ResourceGroup"
$raExists = az role assignment list --assignee $AppId --scope $scope `
    --query "[?roleDefinitionName=='Contributor'] | [0].id" -o tsv
if (-not $raExists) {
    az role assignment create --assignee $AppId --role Contributor --scope $scope -o none 2>$null
    if ($LASTEXITCODE -ne 0) {
        # New service principals can take a few seconds to be resolvable.
        Write-Host '    identity not resolvable yet, retrying in 20s...' -ForegroundColor Yellow
        Start-Sleep -Seconds 20
        az role assignment create --assignee $AppId --role Contributor --scope $scope -o none
        Assert-LastExit 'create role assignment'
    }
    Write-Host "    Contributor granted on '$ResourceGroup'"
} else {
    Write-Host '    role assignment already present'
}

# --------------------------------------------------------------------------
if ($SkipGitHub) {
    Write-Host '==> 5/6 GitHub wiring skipped (-SkipGitHub)' -ForegroundColor Yellow
    Write-Host '    Add these in the repo yourself (Settings -> Secrets and variables -> Actions):'
    Write-Host "      secret   AZURE_CLIENT_ID       = $AppId"
    Write-Host "      secret   AZURE_TENANT_ID       = $TenantId"
    Write-Host "      secret   AZURE_SUBSCRIPTION_ID = $SubId"
    Write-Host "      variable AZURE_WEBAPP_NAME     = $AppName"
    Write-Host "      variable APP_URL               = https://$AppName.azurewebsites.net"
} else {
    Write-Host "==> 5/6 GitHub repo '$GitHubRepo'" -ForegroundColor Cyan
    gh api --method PUT "repos/$GitHubRepo/environments/production" --silent 2>$null  # idempotent PUT
    gh secret   set AZURE_CLIENT_ID       --repo $GitHubRepo --body $AppId;    Assert-LastExit 'set AZURE_CLIENT_ID'
    gh secret   set AZURE_TENANT_ID       --repo $GitHubRepo --body $TenantId; Assert-LastExit 'set AZURE_TENANT_ID'
    gh secret   set AZURE_SUBSCRIPTION_ID --repo $GitHubRepo --body $SubId;    Assert-LastExit 'set AZURE_SUBSCRIPTION_ID'
    gh variable set AZURE_WEBAPP_NAME     --repo $GitHubRepo --body $AppName;  Assert-LastExit 'set AZURE_WEBAPP_NAME'
    gh variable set APP_URL               --repo $GitHubRepo --body "https://$AppName.azurewebsites.net"; Assert-LastExit 'set APP_URL'
    Write-Host '    3 secrets + 2 variables set (all non-sensitive identifiers)'

    if (-not $SkipPushProtection) {
        Write-Host '==> 6/6 Secret-scanning push protection' -ForegroundColor Cyan
        $body = '{"security_and_analysis":{"secret_scanning_push_protection":{"status":"enabled"}}}'
        $body | gh api --method PATCH "repos/$GitHubRepo" --input - --silent 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host '    could not enable automatically (needs admin; free on public repos).' -ForegroundColor Yellow
            Write-Host '    Enable in Settings -> Code security -> Push protection.' -ForegroundColor Yellow
        } else {
            Write-Host '    enabled'
        }
    }
}

# --------------------------------------------------------------------------
Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host "  App URL : https://$AppName.azurewebsites.net"
if ($SkipGitHub) {
    Write-Host '  Next    : add the secrets/variable above, then run the deploy workflow.'
} else {
    Write-Host "  Deploy  : push to main, or  gh workflow run 'Deploy to Azure' --repo $GitHubRepo"
}
Write-Host '  OIDC federation only - no publish profile, no client secret, nothing long-lived.'
