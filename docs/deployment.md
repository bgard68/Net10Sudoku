[← Back to main README](../README.md)

# Azure deployment (Free F1)

This deploys the Blazor Server app to a single **Azure App Service on the Free
(F1) tier** from GitHub Actions. It follows the rules already set out in
[security.md](security.md#azure-deployment): authenticate with **OIDC**, keep
all configuration in **App Settings**, and never commit or store a long-lived
credential.

## The "no secrets" guarantee

Nothing sensitive is committed to the repository, and GitHub stores no reusable
credential. Deployment auth is a short-lived token minted per run:

| Where | What lives there | Sensitive? |
|---|---|---|
| Repo files | source, `appsettings.json`, `appsettings.Development.json`, both workflows | No — audited, no secrets |
| GitHub **secrets** | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | No — plain identifiers, useless without the Azure-side trust |
| GitHub **variables** | `AZURE_WEBAPP_NAME` | No — the public hostname stem |
| Azure App Settings | `ASPNETCORE_ENVIRONMENT`, `AllowedHosts`, anything runtime | Kept server-side, never in the repo |
| Nowhere | publish profile, `AZURE_CREDENTIALS`, client secret, `.pubxml` | — these are deliberately **not** used |

The three IDs are not secrets in the cryptographic sense — they identify the
app registration, directory and subscription. They can do nothing on their own:
Azure only issues a token when a request presents a valid GitHub OIDC assertion
whose `subject` matches the federated credential configured below. They are
stored as GitHub *secrets* purely by convention (so they are masked in logs).

## Prerequisites

- An Azure subscription (the Free tier plan costs nothing).
- The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
  (`az`) and the [GitHub CLI](https://cli.github.com/) (`gh`), both logged in:
  `az login` and `gh auth login`.
- Owner or User Access Administrator on the subscription (needed once, to
  create the role assignment in step 4).

## Provision with the script (recommended)

`tools/azure-provision.ps1` performs the entire one-time setup below in a single
idempotent run: resource group, Free F1 plan, web app (WebSockets, HTTPS-only,
App Settings, pinned startup command), the OIDC deploy identity + federated
credential + resource-group-scoped role, and the GitHub secrets and variables.
Re-running is safe — existing resources are detected and reused.

From a local PowerShell in the repo root, signed in to both CLIs (see
Prerequisites above):

```powershell
.\tools\azure-provision.ps1 -AppName <app-name>
```

`<app-name>` must be **globally unique** — it becomes
`https://<app-name>.azurewebsites.net`. Check a candidate first, without
creating anything:

```powershell
$sub = az account show --query id -o tsv
az rest --method post `
  --url "https://management.azure.com/subscriptions/$sub/providers/Microsoft.Web/checknameavailability?api-version=2023-12-01" `
  --body '{\"name\":\"<app-name>\",\"type\":\"Microsoft.Web/sites\"}'
```

With just `-AppName`, the script auto-detects the rest: the **GitHub repo** from
this repo's git remote (via `gh`, then `git`), the **resource group**
(`rg-<repo>`) and **plan** (`plan-<repo>-free`) derived from the repo name, the
**location** adopted from the resource group if it already exists (otherwise
`centralus`), and the **subscription** from your `az` login. Override any of
them explicitly when you want: `-GitHubRepo owner/repo`, `-ResourceGroup <name>`,
`-PlanName <name>`, `-Location <region>`, `-SubscriptionId <id>`. Other flags:
`-SkipGitHub` (do the Azure side and print the IDs instead of setting them) and
`-SkipPushProtection`. When it finishes it prints the app URL; confirm the
GitHub side with `gh secret list` and `gh variable list`.

### Choose a region that has Free-tier quota

F1 compute quota is **per region**, and a subscription can have a limit of `0`
in a given region — `az appservice plan create` then fails with *"Operation
cannot be completed without additional quota … Current Limit (Total VMs): 0"*.
That is a regional quota, not a billing problem: pass `-Location` for a region
where the subscription has quota (a region that already hosts a working App
Service is a safe bet), or request an increase under Portal → **Quotas**. A
resource group's location cannot change after creation, so if you switch regions
on a re-run, delete the empty group first
(`az group delete --name <resource-group> --yes`) — otherwise the script adopts
the group's existing location.

## Doing it by hand (what the script automates)

If you would rather run each step yourself, these are the same steps the script
performs. Run them from any shell. Fill in the four values at the top
(`GH_REPO`, `NAME`, `APP`, `LOCATION`); `RG` and `PLAN` derive from `NAME`,
mirroring the script. Set `LOCATION` to a region with F1 quota (see the note
above), and `APP` must be **globally unique** — it becomes
`https://<APP>.azurewebsites.net`.

```bash
GH_REPO=<owner>/<repo>              # your repo, e.g. your-name/your-repo
NAME=<project>                      # short slug for resource names, e.g. myapp
APP=<app-name>                      # globally unique -> https://<app-name>.azurewebsites.net
LOCATION=<region>                   # a region where your subscription has F1 quota
RG=rg-$NAME
PLAN=plan-$NAME-free
```

### 1. Create the Free-tier web app

```bash
az group create --name "$RG" --location "$LOCATION"

# F1 is the free SKU; it must be a Linux plan for the .NET 10 runtime below.
az appservice plan create --name "$PLAN" --resource-group "$RG" \
  --sku F1 --is-linux

# Confirm the exact runtime token first if this errors:
#   az webapp list-runtimes --os linux | grep -i dotnet
az webapp create --name "$APP" --resource-group "$RG" --plan "$PLAN" \
  --runtime "DOTNETCORE:10.0"

# Blazor Server uses a SignalR WebSocket; enable it and force HTTPS.
az webapp config set    --name "$APP" --resource-group "$RG" --web-sockets-enabled true
az webapp update        --name "$APP" --resource-group "$RG" --https-only true
```

### 2. Configuration — App Settings, not files

```bash
az webapp config appsettings set --name "$APP" --resource-group "$RG" --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  AllowedHosts="$APP.azurewebsites.net" \
  SCM_DO_BUILD_DURING_DEPLOYMENT=false

# Pin the startup command so there is no ambiguity about what boots.
az webapp config set --name "$APP" --resource-group "$RG" \
  --startup-file "dotnet Sudoku.dll"
```

`ASPNETCORE_ENVIRONMENT=Production` is what turns on HSTS and HTTPS redirection
in `Program.cs`. Setting `AllowedHosts` to the real hostname replaces the `*`
development default, as [security.md](security.md#azure-deployment) requires.
This app has no database, so there is no connection string and no other setting
to add here. Nested keys, if you ever add any, use double underscores
(`Logging__LogLevel__Default`). Do **not** set `Always On` — the Free tier does
not support it.

`SCM_DO_BUILD_DURING_DEPLOYMENT=false` matters: the workflow deploys a
pre-built `dotnet publish` package, so Azure/Oryx must **not** try to rebuild it
in place. Leaving it on is the classic cause of a half-populated web root that
boot-loops.

### What actually lands in the web root

The deploy pushes the **publish output**, not the repository. `dotnet publish`
emits a self-contained, runnable folder — `Sudoku.dll` (the entry assembly),
its dependency DLLs, `Sudoku.runtimeconfig.json`, `Sudoku.deps.json`, the app's
own `wwwroot/` static assets, and the non-secret `appsettings*.json`. No `.cs`
source, no `.csproj`, no `bin/`/`obj/`, none of the other projects. The
`azure/webapps-deploy` step unzips the *contents* of that folder into
`/home/site/wwwroot`, so `Sudoku.dll` sits at the site root (where the Linux
.NET container looks for it) and the app's static files sit at
`/home/site/wwwroot/wwwroot` (where ASP.NET serves them from). The workflow's
`Verify publish output` step fails the run if `Sudoku.dll` is not at that root,
so a malformed package can never reach the site.

### 3. Create the deploy identity and trust this repo

```bash
# App registration GitHub will act as (no client secret is ever created).
APP_ID=$(az ad app create --display-name "gh-$NAME-deploy" --query appId -o tsv)
az ad sp create --id "$APP_ID"

# Federated credential: trust tokens from this repo's 'production' environment.
# The subject MUST match how the deploy job runs. deploy.yml pins the job to
# 'environment: production', so the OIDC subject is the environment form below
# (NOT a branch ref) — this is the single most common setup mistake.
az ad app federated-credential create --id "$APP_ID" --parameters "{
  \"name\": \"gh-$NAME-production\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:${GH_REPO}:environment:production\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"
```

### 4. Grant it permission to deploy (scoped)

```bash
SUB_ID=$(az account show --query id -o tsv)
az role assignment create --assignee "$APP_ID" \
  --role "Contributor" \
  --scope "/subscriptions/$SUB_ID/resourceGroups/$RG"
```

Contributor scoped to the resource group is the least privilege that lets
`azure/webapps-deploy` fetch the app's deploy endpoint. Keeping the web app in
its own resource group (as above) means this grant reaches nothing else.

### 5. Hand GitHub the three IDs and the app name

```bash
TENANT_ID=$(az account show --query tenantId -o tsv)

# Create the environment the deploy job targets (so the subject above resolves).
gh api --method PUT "repos/$GH_REPO/environments/production" >/dev/null

gh secret   set AZURE_CLIENT_ID       --repo "$GH_REPO" --body "$APP_ID"
gh secret   set AZURE_TENANT_ID       --repo "$GH_REPO" --body "$TENANT_ID"
gh secret   set AZURE_SUBSCRIPTION_ID --repo "$GH_REPO" --body "$SUB_ID"
gh variable set AZURE_WEBAPP_NAME     --repo "$GH_REPO" --body "$APP"
gh variable set APP_URL               --repo "$GH_REPO" --body "https://$APP.azurewebsites.net"
```

`APP_URL` is the site root; the `keep-warm.yml` workflow reads it to ping the
app (see below). It is a plain public URL, not a secret.

### 6. Turn on secret-scanning push protection

On a public repo it is free and rejects a push containing a recognised
credential before it reaches the remote — the best backstop for the "never
introduce the first secret" rule.

```bash
gh api --method PATCH "repos/$GH_REPO" \
  -f 'security_and_analysis[secret_scanning_push_protection][status]=enabled' >/dev/null
```

(Or Settings → Code security → *Push protection*.)

## Deploy

Push to `main`, or trigger it by hand:

```bash
gh workflow run "Deploy to Azure" --repo "$GH_REPO"
```

`deploy.yml` restores, builds, runs the full test suite, publishes, and only
then deploys — so `main` can never ship a red build. Watch it with
`gh run watch`. When it is green, open `https://<APP>.azurewebsites.net`.

## Free-tier caveats worth knowing

The F1 tier is genuinely free but constrained, and two constraints interact
with how this app works:

- **It idles out and cold-starts.** No *Always On*, ~60 CPU-minutes/day, one
  shared instance. After a period of no traffic the app unloads; the next
  request pays a few seconds of cold start. The included **`keep-warm.yml`**
  workflow mitigates this: it pings `APP_URL` every ~10 minutes so the instance
  stays loaded. With no database, the ping only warms the web server. It no-ops
  until `APP_URL` is set (the provisioning script sets it), and on a public repo
  the Actions minutes are free.
- **Saved games can reset on that unload.** Games are stored in the browser but
  encrypted with ASP.NET Core Data Protection, whose keys default to the local
  filesystem. When the instance recycles, the key ring can change and older
  saves silently fail to decrypt (`GameStorage` reports "no saved game"). On a
  single F1 instance this is the *restart* case, not scale-out. It is harmless
  for casual play; if it matters, persist the keys to Blob Storage as shown in
  [security.md](security.md#one-correctness-trap-specific-to-this-app). Do not
  scale past one instance on any tier without doing this first.

Upgrading to Basic (B1) later removes the idle/CPU limits and adds Always On;
nothing in this setup changes except the plan SKU.

See also: [security](security.md) · [architecture](architecture.md) ·
[testing](testing.md)

[← Back to main README](../README.md)
