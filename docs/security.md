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
- Actions are pinned to **full commit SHAs**, with the human-readable tag in a
  trailing comment, and kept current by Dependabot
  (`.github/dependabot.yml`), which also watches NuGet. A tag is mutable:
  whoever controls the action repository can repoint `v4` at new code that runs
  inside this pipeline with its token. A SHA cannot be repointed.
- Both jobs carry `timeout-minutes`.

## Runtime hardening

Every response carries these, set in `Program.cs` and asserted by
`SecurityPostureTests` on every `dotnet test`:

| Header | Value | Why |
|---|---|---|
| `Content-Security-Policy` | `default-src 'self'`, `script-src 'self'`, `object-src 'none'`, `base-uri 'self'`, `form-action 'self'`, `frame-ancestors 'self'` | Constrains where scripts may come from. `style-src` carries `'unsafe-inline'` because the board colour pickers write `style` attributes, which no nonce or hash can cover; `script-src` deliberately does not. |
| `X-Content-Type-Options` | `nosniff` | Stops a browser second-guessing a declared content type. |
| `X-Frame-Options` | `SAMEORIGIN` | Legacy clickjacking defence alongside `frame-ancestors`. |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Keeps the visited path off third-party sites. |
| `Permissions-Policy` | camera, microphone, geolocation, payment, USB all `()` | A Sudoku board needs no device capabilities. |
| `Strict-Transport-Security` | 30 days (non-Development only) | HSTS must never be sent from localhost. |

The headers are written from a `Response.OnStarting` callback rather than
inline, because the framework appends its own `frame-ancestors` policy while
rendering - setting ours up front left every response carrying **two**
`Content-Security-Policy` headers. Browsers intersect duplicates, so the result
was accidentally correct and needlessly hard to audit. A test now asserts
exactly one is sent.

Other runtime controls:

- **Forwarded headers** (`X-Forwarded-Proto`, `X-Forwarded-For`) are processed.
  App Service terminates TLS and forwards over plain HTTP, so without this the
  app believes every request is insecure - which silently drops `Secure` from
  cookies and makes the client IP the proxy's address for everyone.
- **Antiforgery cookie**: `HttpOnly`, `SameSite=Strict`, and `Secure` outside
  Development. See the trap below before changing this.
- **Rate limiting**: a fixed window of 240 requests per minute partitioned by
  client IP, with the circuit's long-lived WebSocket exempted. Generating a
  Professional puzzle is CPU-bound work any anonymous visitor can trigger, and
  the Free tier has a daily CPU quota - exhausting it suspends the site.
- **Circuit caps**: 20 disconnected circuits retained for 2 minutes (defaults:
  100 for 3 minutes) and a 32 KB maximum received message, bounding what
  abandoned or hostile connections hold on a 1 GB instance.
- **`AllowedHosts`** is an explicit allow-list, not `*`.

### The trap: `CookieSecurePolicy.Always` throws on plain HTTP

The obvious fix for a cookie missing `Secure` is to set `SecurePolicy = Always`
unconditionally. Do not. ASP.NET Core's antiforgery system **throws**
`InvalidOperationException` when that policy is set and the request is not
actually SSL, turning every POST into a 500. Development therefore keeps
`SameAsRequest`, and production relies on forwarded headers making the request
appear as HTTPS. Both halves are pinned by tests - see
[lessons learned](lessons-learned.md#the-secure-cookie-fix-that-broke-every-post).

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
- **`AllowedHosts` is an explicit allow-list**, both in `appsettings.json` and
  as an App Setting the provisioning script writes. `*` accepts any `Host`
  header, which permits host-header injection and cache poisoning. A wrong
  value makes every request `400` - the post-deploy smoke test catches that
  immediately rather than letting it sit.
- HTTPS redirection and HSTS are already enforced outside Development
  (`Program.cs`), which is what App Service terminates TLS in front of.
- **`min-tls-version 1.2` and `ftps-state Disabled`** are set explicitly by
  the provisioning script rather than inherited from whatever the platform
  defaults to this year. Deployment goes through the OIDC workflow, so the FTP
  endpoint is only an extra credential-bearing way in.
- **The deploy identity holds `Website Contributor`**, scoped to the resource
  group - not `Contributor`. Both can publish the app; only the latter can
  create and delete arbitrary resources. If the federated token were ever
  misused, that is the difference between "redeploy the site" and "rebuild the
  estate". Apps provisioned before this change keep the older, broader
  assignment; the script now flags it with the command to remove it.

### Still open

- **GitHub secret scanning and push protection are not enabled.** Both are free
  for public repositories. Push protection is the one control that would block
  an accidental credential commit at `git push` time, which is exactly what the
  hygiene above currently depends on humans not doing. Enable under
  *Settings → Code security and analysis*.

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
