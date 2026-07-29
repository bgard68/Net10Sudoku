[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)

# Lessons: hosting, tooling and deployment

Configuration, cross-platform scripting and the Azure rollout - failures that had nothing to do with the game logic.

## Hosting and configuration

### Total outage with a green test suite
**Bug:** Running the app outside the Development environment crashed every
page with `FileNotFoundException` on the static-asset bundle - while all
unit tests passed, because unit tests never boot the host.
**Fix / mitigation:** Documented the environment requirement; added an HTTP
smoke test that boots the built DLL and fetches those exact assets, run in
CI on every push ([details](../testing.md)).
**Lesson:** Unit tests cannot see hosting. Something in the pipeline must
actually start the application.

### Dead controls on the HTTP launch profile
**Bug:** Started on the default `http` profile (port 5260) the page rendered,
but nothing worked - no puzzle appeared and every button was inert.
`UseHttpsRedirection()` ran unconditionally and antiforgery kept its default
`Secure` cookie policy, so over plain HTTP the cookie was never sent back and
the interactive circuit was rejected with a 400.
**Found:** Reading `Program.cs` against `launchSettings.json` - the default
profile is HTTP-only while the pipeline assumed HTTPS.
**Fix:** `UseHttpsRedirection()` moved inside the non-Development branch, and
antiforgery uses `CookieSecurePolicy.SameAsRequest` in Development.
**Verified:** Loaded `http://localhost:5260` in a real browser: a puzzle
generated, the clock ticked, Notes toggled, digits placed. None of that
happens without a live circuit, so a playable board *is* the assertion.
**Lesson:** A statically-rendered Blazor page is pixel-identical to a live one
until you interact with it. "The page loads" is not "the app works".

### Blazor renders after async handlers - use it
**Observation:** "Generating..." feedback needs no manual re-render
plumbing: an async event handler yields at its first `await` and Blazor
renders the intermediate state automatically.
**Lesson:** Know the framework's render points before adding notification
machinery.

## Cross-platform tooling

### PowerShell is two platforms wearing one syntax
**Bugs (both caught by CI, not local runs):**
1. `Start-Process -WindowStyle` does not exist on Linux/macOS PowerShell -
   the smoke test died at app startup on the Ubuntu runner.
2. For non-2xx responses, the error's content type lives at
   `HttpWebResponse.ContentType` on Windows PowerShell 5.1 but at
   `Content.Headers.ContentType` on PowerShell 7 - a 404 check passed on
   Windows and failed on Linux.

**Fixes:** Platform-conditional parameter splatting; reading whichever
response shape is presented.
**Lesson:** "Works in my shell" is not "works in PowerShell". If a script
targets CI, its first CI run *is* the test - and a CI leg on a different OS
is itself a check worth having, because it caught both of these.

### An exact SDK pin that only exists on one machine
**Bug:** `global.json` pinned an exact preview build number that no CI
runner would ever have installed.
**Fix:** Pin by feature-band floor with `rollForward: latestFeature`.
**Lesson:** Pin for reproducibility, but pin something the rest of the
world can resolve.

## Deployment (Azure, Free F1)

### Free-tier compute quota is per region, and can be zero
**Bug:** Creating the F1 App Service plan failed with *"Operation cannot be
completed without additional quota … Current Limit (Total VMs): 0"* in the
first region tried. Nothing was misconfigured and nothing was billable — the
subscription simply had a regional vCPU limit of zero there.
**Fix:** Provision in a region where the subscription has quota (`-Location`);
a region that already hosts a working App Service is a safe bet. A quota
increase can also be requested under Portal → Quotas.
**Lesson:** "Free" does not mean "available everywhere". Compute quota is a
per-region limit, not a billing state, and a subscription can have zero in one
region while another works. Read the error for the *region*, not just the number.

### A resource group's location cannot change, so an idempotent re-run broke
**Bug:** After switching regions on the retry, provisioning died at step 1 with
`InvalidResourceGroupLocation: The Resource group already exists in location
'<old-region>'`. The script called `az group create` with the new `-Location`
unconditionally, and Azure rejects a location change on an existing group.
**Fix:** Check first (`az group exists`) and reuse an existing group as-is; only
create when it is genuinely absent. To actually move regions, delete the empty
group first.
**Lesson:** Idempotency is not "call create again". A resource with an immutable
property — a group's location — makes a blind re-create a hard error; a
re-runnable script has to detect existence and skip, not re-assert.

### An existence check built on a command that errors when absent
**Bug:** The script probed for the web app with `az webapp show`, which *errors*
when the app does not exist yet — which is exactly the first-run case. Under
PowerShell 7.4 defaults (`$PSNativeCommandUseErrorActionPreference` is `$true`)
together with `$ErrorActionPreference = 'Stop'`, that native error became
terminating and aborted the whole run mid-provision — even with the command's
stderr redirected to `$null`.
**Fix:** Probe with `az … list --query "[?name=='…']"`, which returns an empty
string instead of erroring, matching every other existence check in the script;
and set `$ErrorActionPreference = 'Continue'` with
`$PSNativeCommandUseErrorActionPreference = $false`, gating real failures on
`$LASTEXITCODE` explicitly.
**Lesson:** Don't build an existence check on a command whose "not found" is an
error — prefer a query that returns empty. And PowerShell 7.4 changed native
error handling: a non-zero exit now throws under `Stop` by default, so a script
that shells out must choose its error mode deliberately, not inherit it.

See also: [architecture review](../architecture-review.md) ·
[testing](../testing.md)


[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)
