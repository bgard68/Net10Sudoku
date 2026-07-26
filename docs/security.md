[← Back to main README](../README.md)

# Security posture

This is a public repository with no database, no accounts and no user data
beyond what a player's own browser stores. That keeps the threat model small,
but it also means every commit is world-readable forever - so the rules below
are about never introducing the first secret, rather than cleaning one up.

## What is in the repository

| Checked in | Deliberately not |
|---|---|
| `appsettings.json` - non-secret baseline (log levels, `AllowedHosts`) | `appsettings.Production.json`, `.Staging.json`, `.local.json` |
| `appsettings.Development.json` - shared dev log levels | `secrets.json`, `.env` and `.env.*` |
| `launchSettings.json` - localhost ports, `ASPNETCORE_ENVIRONMENT` | `*.pubxml` publish profiles (they carry deploy credentials) |
| `global.json`, `Directory.Build.props`, `.github/workflows/ci.yml` | `serviceDependencies*.json`, `*.cscfg`, `ApplicationInsights.config` |
| Source, tests, docs | `bin/`, `obj/`, `.vs/`, `TestResults/`, logs, `*.key`, `*.pfx` |

Audited over the full history (every commit on every branch): no cloud keys,
tokens, JWTs, private keys or connection strings have ever been committed, and
no build output or logs. The only configuration files ever added are the six
listed above.

Two of those files are committed by design and are therefore the places a
secret would actually escape from: **`appsettings.Development.json`** and
**`launchSettings.json`**. `.gitignore` cannot protect a tracked file. Nothing
sensitive belongs in either.

## Where secrets go instead

- **Development** - `dotnet user-secrets`. The project carries a
  `UserSecretsId`, so values live under the user profile, outside the working
  tree, and override `appsettings.*` automatically.
- **Production** - App Service Application Settings, or Key Vault for anything
  genuinely sensitive. Never a checked-in `appsettings.Production.json`.
  Nested keys use double underscores: `Logging__LogLevel__Default`.
- **GitHub** - enable secret-scanning **push protection**. On a public
  repository it is free, and it rejects a push containing a recognised
  credential before it ever reaches the remote. That is worth more than any
  after-the-fact scan.

## Continuous integration

- `permissions: contents: read` - the default token gets read access and
  nothing else.
- The workflow triggers on `pull_request`, **not** `pull_request_target`. A
  fork's pull request therefore runs with no repository secrets and no write
  access. This distinction is the difference between a safe public CI and a
  remote-code-execution path into the repository.
- No `secrets.*` are referenced, so there is nothing for a malicious PR to
  exfiltrate, and no `github.event` value is interpolated into a `run:` block,
  so there is no script-injection surface.
- `persist-credentials: false` on checkout keeps the job token out of
  `.git/config` on the runner.
- Actions are pinned by major tag and kept current by Dependabot
  (`.github/dependabot.yml`), which also watches NuGet.
- Both jobs carry `timeout-minutes`.

## Azure deployment

Established — see the step-by-step [deployment guide](deployment.md) (Free F1,
GitHub Actions). The rules that keep the above true, and that the guide
implements:

- **Authenticate with OIDC**, not a stored credential. `azure/login` with a
  federated credential needs `permissions: id-token: write` on the deploy job
  and stores nothing long-lived. A publish profile or an `AZURE_CREDENTIALS`
  secret is a standing key with no expiry - avoid both.
- **Configuration comes from App Settings / Key Vault**, never from a file in
  the repository.
- **Restrict `AllowedHosts`** to the real hostname once it exists; `*` is a
  development default.
- HTTPS redirection and HSTS are already enforced outside Development
  (`Program.cs`), which is what App Service terminates TLS in front of.

### One correctness trap specific to this app

Saved games are encrypted with ASP.NET Core Data Protection
([persistence](architecture.md#persistence-there-is-no-database)). The default
key store is the local filesystem, which on App Service does not reliably
survive restarts, slot swaps or scale-out, and differs per instance. Left
alone, players silently lose saved games - intermittently once there is more
than one instance, because `GameStorage` swallows the decryption failure and
reports "no saved game".

Persist the keys before scaling past one instance:

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(blobUri, credential)
    .ProtectKeysWithAzureKeyVault(keyUri, credential);
```

Blazor Server also holds a stateful SignalR circuit per user, so scale-out
needs ARR affinity (on by default in App Service) or Azure SignalR Service.

See also: [architecture](architecture.md) · [testing](testing.md) ·
[lessons learned](lessons-learned.md)

[← Back to main README](../README.md)
