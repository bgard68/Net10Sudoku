[← Back to main README](../README.md)

# Lessons learned

Every bug below actually happened in this project. Each entry records the
defect, the fix, and the transferable lesson. The full detail lives in the
git history - each fix landed as its own commit with reasoning in the
message.

## What catches each of these now

A lesson nobody can regress against is a story, not a control. This table maps
every defect to the thing that would catch it today - and, honestly, names the
ones where that thing is a human following a procedure rather than a test.

| Defect | What detects it now |
|---|---|
| Hints broken by a wrong entry | `HintTests` - hint offered for every empty cell after a deliberate wrong entry |
| Difficulty labels not matching technique | `PuzzleGraderTests.Generated_puzzles_land_in_their_difficulty_band` |
| Medium fallback handing out an Advanced board | Same band test - Medium must never require advanced techniques |
| Undo not reverting swept notes atomically | `UndoRedoAndNotesTests.Undo_restores_swept_notes` |
| Mistake count forgiven by undo | `MistakeTests.Undo_does_not_forgive_a_mistake` |
| Auto-solve setting a best time | `GameSessionTests.An_auto_solved_win_never_sets_a_record` |
| Corrupt save breaking startup | `GameSessionTests.Initialize_falls_back_to_a_new_game_when_the_snapshot_is_corrupt` |
| Fast solver disagreeing with the naive one | `SudokuSolverTests.Fast_counter_agrees_with_the_reference_counter` |
| Total outage with green unit tests | Smoke test - boots the built DLL and fetches the real assets, in CI and again against the live URL after deploy |
| Antiforgery cookie losing `Secure` behind the proxy | `SecurityPostureTests.Antiforgery_cookie_is_secure_and_locked_down_in_production` + live smoke check |
| `SecurePolicy.Always` 500-ing every POST | `SecurityPostureTests.Antiforgery_cookie_policy_does_not_break_plain_http_development` + the smoke test's POST check that first caught it |
| Forwarded headers removed | `SecurityPostureTests.Forwarded_headers_are_processed` |
| Duplicate CSP header | `SecurityPostureTests.Exactly_one_content_security_policy_header_is_sent` + smoke check |
| Security headers dropped | Five `SecurityPostureTests` assertions + the smoke test's header block |
| `AllowedHosts` widened to `*`, or narrowed to nothing | Paired `Unknown_host_headers_are_rejected` / `The_configured_host_is_still_served` |
| Error pages leaking stack traces | `SecurityPostureTests.Not_found_responses_do_not_leak_diagnostics` |
| An inline handler the CSP blocks | `FrontendPostureTests.Markup_contains_no_inline_event_handler_attributes` |
| A stylesheet or script that 404s | `FrontendPostureTests.Every_referenced_static_asset_exists` |
| A class collision making a control click-transparent | `FrontendPostureTests` - the generic-class `pointer-events` rule, plus the pencil-marks/Notes check by name |
| Mojibake from a Windows-1252 save | `FrontendPostureTests.No_source_file_contains_mojibake_from_a_bad_encoding_save` |
| PowerShell edition differences in tooling | The CI smoke-test job runs on Ubuntu/pwsh - both edition bugs were caught there, not locally |
| An SDK pin no runner can resolve | Any CI run - it fails at `setup-dotnet` |
| **A board colour combination nobody can read** | **Runtime guard, not a test**: the colour panel measures live contrast and warns with a repair. Contrast itself is browser-verified, because compositing translucent layers is a rendering job |
| **CSS transitions, overlay alignment, flex sizing, picker anchoring, touch drags** | **Browser verification only** - the procedure is written down in [testing](testing.md#browser-verification). No automated guard; these need a real rendering engine |
| **Timer callbacks off the sync context** | **Code review only** - threading correctness here has no cheap assertion |
| **A false finding from a misread API or a bad measurement** | **Procedure only** - verify in the system that owns the setting, and sanity-check surprising numbers before reporting |

The bolded rows are the honest gaps: real defects with no automated detection.
They are the argument for keeping the browser-verification checklist rather
than trusting a green build.

## Correctness

### One wrong digit disabled every hint
**Bug:** Hints were computed by re-solving the *live* board, player entries
included. A single wrong digit anywhere made the board unsolvable, so every
hint request returned nothing - measured at 0 of 39 empty cells receiving a
hint. Worse, asking for a hint on a wrongly-filled cell echoed the wrong
value back, because the solver skips filled cells.
**Fix:** Record the completed grid at generation time; hints read it in O(1).
**Lesson:** Never derive ground truth from state the user can corrupt. If a
correct answer exists at creation time, store it.

### Difficulty labels that measured the wrong thing
**Bug:** Difficulty was implemented as clue-removal count. Half of "Hard"
boards fell to singles; "Professional" was no harder than Hard.
**Fix:** Grade every candidate by the techniques it demands; regenerate
until the grade lands in the band ([details](puzzle-generation.md)).
**Lesson:** A proxy metric will quietly diverge from the thing it stands
for. Measure the real property, even if it costs a retry loop.

### The Medium fallback could hand out an impossible board
**Bug:** When every carving attempt missed the Medium band, the
closest-miss fallback treated "one tier too easy" and "one tier too hard"
as equally close - roughly one Medium in a thousand required chain
techniques to finish.
**Fix:** The distance function scores the hard side as far away, so a
fallback Medium only ever errs easy. Pinned by a test.
**Lesson:** Fallback paths need design, not just existence - and asymmetric
costs need asymmetric distance functions. Also: a rare test flake is
sometimes a real product bug at low probability, not test noise. Chase it.

### A singleton theme in a multi-user host
**Bug:** `ThemeService` was registered as a singleton in Blazor Server,
where a singleton spans every connected circuit - one player toggling dark
mode changed it for everyone.
**Fix:** Scoped registration (per circuit).
**Lesson:** Service lifetime is a correctness decision in server-rendered
UI frameworks, not a performance knob.

## Hosting and configuration

### Total outage with a green test suite
**Bug:** Running the app outside the Development environment crashed every
page with `FileNotFoundException` on the static-asset bundle - while all
unit tests passed, because unit tests never boot the host.
**Fix / mitigation:** Documented the environment requirement; added an HTTP
smoke test that boots the built DLL and fetches those exact assets, run in
CI on every push ([details](testing.md)).
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

## Concurrency

### Timer callbacks off the renderer's context
**Bug:** A `System.Timers.Timer` callback mutated component state on a
timer thread before marshalling to Blazor's sync context.
**Fix:** The whole callback body runs inside `InvokeAsync`.
**Lesson:** In Blazor Server, *all* component state access belongs on the
sync context - including the reads and increments that look harmless.

### A clock that started before the circuit did
**Bug:** The game timer was created and started in `OnInitialized`, which also
runs during server prerender. That prerendered instance is torn down straight
away, so its timer could fire against disposed state on a timer thread.
**Found:** Reviewing the component lifecycle rather than from a crash - the
window is narrow enough that it rarely bites, which is what makes it worth
fixing before it does.
**Fix:** The timer is created in `OnAfterRenderAsync(firstRender)`, alongside
`Session.InitializeAsync()`. Both need a live interactive circuit; neither
belongs in prerender.
**Lesson:** `OnInitialized` runs twice in a prerendered Blazor Server app.
Anything with a lifetime - timers, subscriptions, JS interop - belongs in the
first *interactive* render, not the first render.

## Security hardening

These came out of a DevOps security review of the pipeline, the cloud posture
and the runtime. Two of them are bugs the review *introduced* while fixing
something else, which is the more useful half of the story.

### The reverse proxy that silently unsecured a cookie
**Bug:** The antiforgery token cookie was served without the `Secure`
attribute. Probing production showed `httponly` and `samesite=strict` present,
`secure` absent - while Azure's own affinity cookie in the same response had
it, which is what made the omission obvious rather than theoretical.

**Root cause:** App Service terminates TLS and forwards to Kestrel over plain
HTTP. The app never called `UseForwardedHeaders`, so `Request.IsHttps` was
`false` inside the process, and the default `SameAsRequest` cookie policy
faithfully declined to mark the cookie secure. Nothing was misconfigured in the
cookie code; the app simply could not see that it was serving HTTPS. The same
blindness would have made any IP-partitioned rate limiting meaningless, since
every client would have looked like the proxy.

**Fix:** Process `X-Forwarded-Proto` and `X-Forwarded-For`, and set the cookie
policy to `Always` outside Development.

**Lesson:** Behind a TLS-terminating proxy, "is this request secure?" is not a
question the app can answer on its own. Configure forwarded headers *first*;
several unrelated-looking defects downstream are really this one.

### The Secure-cookie fix that broke every POST
**Bug (self-inflicted, caught before it shipped):** Setting
`options.Cookie.SecurePolicy = CookieSecurePolicy.Always` unconditionally
turned every POST into a **500**.

**How it was found:** The smoke test's existing "POST to a page route is
rejected (405 or 404)" check went red immediately after the change - it had
been green on the previous run, so the change was the suspect. Nothing about a
cookie policy suggests a POST failure, so guessing would have been slow.

**How it was diagnosed:** Rather than reason about it, the app was started with
its console redirected to a log, a single `curl -X POST` fired at a page route,
and the log read. The exception named itself exactly:

```
System.InvalidOperationException: The antiforgery system has the configuration
value AntiforgeryOptions.Cookie.SecurePolicy = Always, but the current request
is not an SSL request.
   at Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery.CheckSSLConfig(HttpContext)
```

**Root cause:** ASP.NET Core does not merely skip the `Secure` attribute when
the policy cannot be honoured - it throws. So `Always` is safe only where every
request genuinely is SSL. Locally (plain `http://localhost`) it is not.

**Why this mattered more than a dev-only annoyance:** production reaches
`IsHttps == true` *only because* forwarded headers are configured. Had that
configuration been wrong or removed, the same exception would have fired on the
live site instead of a local one - a hardening change turning into an outage.

**Fix:** `Always` outside Development, `SameAsRequest` inside it. Both halves
are now pinned by tests, so neither can be "simplified" away.

**Lesson:** A security setting that throws when it cannot be satisfied is a
liveness risk, not just a correctness one. Verify hardening in an environment
that actually resembles production, and read the exception instead of inferring
it - the framework named the property, the value and the reason in one line.

### An API error is not evidence a feature is off
**Bug (in the review, not the code):** The security review reported that GitHub
secret scanning and push protection were not enabled, and recommended turning
them on. Both were already on. The claim came from the secret-scanning REST
endpoint returning *"Repository does not have GitHub Advanced Security
enabled"* - which is a statement about the paid GHAS **API surface** on a free
public repository, not about whether scanning is running. Opening
*Settings → Advanced Security* showed Secret Protection, push protection and
CodeQL all active.

**Fix:** Verify platform settings in the settings UI, and record the actual
state in `security.md` rather than an inference.

**Lesson:** A failed API call answers "can I query this?", not "is this
configured?". When a security control is reported missing, confirm it in the
place that owns the setting before writing it down - a false negative in a
security report wastes attention and erodes trust in the rest of the findings.

### One header, sent twice
**Bug:** After adding a Content-Security-Policy, responses carried **two** CSP
headers: the new policy, and a bare `frame-ancestors 'self'` the framework
appends while rendering. Browsers intersect duplicate policies, so the effective
result was correct - by luck.

**Fix:** Write the headers from a `Response.OnStarting` callback so they land
last and overwrite, plus a test asserting exactly one header is sent.

**Lesson:** "It works in the browser" is not the same as "it is what I
declared". Middleware ordering decides who writes last, and a response header
set too early is a suggestion rather than a decision.

## Frontend

### The CSP that silently broke the mobile menu
**Bug:** The nav menu closed itself after a tap via a literal
`onclick="document.querySelector('.navbar-toggler').click()"` attribute.
Inline handlers are script, so the new `script-src 'self'` policy blocked it -
the browser reported `script-src-attr` / `blocked: inline` and the menu simply
stayed open over the board on mobile. Nothing threw; the page looked fine.
**Fix:** A Blazor `@onclick` handler bound to component state. No inline
script, so the policy is satisfied without weakening it.
**Lesson:** Tightening a CSP is a behavioural change, not just a header.
Anything that relied on inline script stops working *silently* - grep for
inline handlers before shipping the policy, and listen for
`securitypolicyviolation` in the browser rather than assuming a clean console
means a clean policy.

### A stylesheet that 404'd on every page load
**Bug:** `App.razor` linked `lib/bootstrap/dist/css/bootstrap.min.css`. The
library was never vendored - the file is not in the repository - so every page
load fetched a 404. It went unnoticed because the app's own scoped CSS styles
every class that looks like Bootstrap (`navbar`, `nav-item`, `container-fluid`).
Related dead weight: `.bi-sun-fill` / `.bi-moon-fill` rules left over from the
removed theme toggle, and two nav icons referencing `.bi-*` classes that were
never defined anywhere, rendering as empty spans.
**Fix:** Removed the link, the dead rules and the phantom icons. A test now
resolves every `@Assets[...]` reference against disk.
**Lesson:** A missing stylesheet fails silently - the page still renders, just
without rules that were never arriving. Template leftovers survive for months
precisely because nothing breaks loudly.

### A colour picker that could make the board invisible
**Bug:** The board colour customisation let a player set near-black numbers on
a near-black grid. Measured contrast: **1.03:1** against the 4.5:1 WCAG AA
minimum - the board was genuinely unreadable. The control panel had been
deliberately protected from this (fixed backdrops), but the board itself had
no such guarantee.
**Fix:** The panel computes the live contrast ratio and shows a warning with
the measured value plus a one-click repair that switches the number colour to
whichever of black or white reads best - leaving the player's chosen grid
colour intact. Warning rather than override: the pick was deliberate, so
inform instead of fighting it.
**Lesson:** Any feature that lets a user choose colours can be driven into an
unusable state. Decide up front whether the app prevents it, warns about it, or
allows it - and if a promise was made about one region of the UI ("the controls
always stay readable"), check whether the rest of the UI inherited that promise.

### Measuring contrast wrong, twice
**Bug (in the review, again in the tooling):** A first contrast sweep reported
seven failing controls including Hint at 1.91:1 and Solve at 2.09:1. Those
numbers were wrong. The helper read `rgba(2, 132, 199, 0.15)` and compared
against `rgb(2,132,199)` - the *unblended* base colour - instead of the result
of compositing that 15%-alpha layer over the panel behind it. Re-measuring with
proper alpha compositing showed only two real failures, and that the disabled
Undo/Redo readings were exempt anyway (WCAG does not apply contrast minimums to
disabled controls).
**Fix:** Composite every background layer up the ancestor chain before
computing the ratio, and skip `disabled` elements.
**Lesson:** Translucent backgrounds are the standard trap in contrast tooling -
`getComputedStyle` hands back the declared value, not the rendered pixel. This
is the second false finding in one review; the pattern is the same each time:
a measurement that *looks* authoritative because it produced a number. Sanity-
check surprising results before reporting them.

## Browser rendering

### The CSS transition that silently ate a feature
**Bug:** The board colour customisation sets CSS custom properties on the
play area (`--grid-bg` and friends), which the cells consume via `var()`.
The grid-background wheel did nothing: cells kept their old colour
indefinitely, even though the variable was verifiably updated on every
ancestor and the stylesheet rule was correct. The culprit was the cells'
`transition: background .2s` - in Chromium, a background transition whose
change arrives through an *inherited custom property* can freeze mid-flight
and never deliver the new value. Two properties on the same element, driven
by the same inline style, behaved differently: `color` (not transitioned)
updated instantly while `background` (transitioned) stayed stale forever.
**Fix:** The cell transition covers `box-shadow` only; background changes
apply instantly.
**Diagnosis path worth remembering:** confirm the variable's computed value
at the target element, enumerate the matching stylesheet rules, reproduce
with an inline style on the same element, reproduce on a *fresh* element
(which worked - the giveaway), then diff what the two elements have -
leaving the transition as the only suspect. Setting `transition: none`
snapped the colour in instantly, confirming it.
**Lesson:** When a style "doesn't apply", the value pipeline can be perfect
and the *animation machinery* can still be the thing eating it. Transitions
belong on properties you intend to animate, not on everything that might
change - and a feature that only manipulates styles still needs live
browser verification, because no compiler or unit test sees this class of
failure.

### The class collision that made a button click-transparent
**Bug:** A player reported the pencil-notes feature as "not implemented" -
clicking the Notes button did nothing. The in-cell pencil-mark overlay used
a CSS class named `notes` with `pointer-events:none` (so marks never block
cell clicks); the Notes *button* also carried a `notes` class, so the bare
`.notes` rule made the button itself click-transparent. Every real mouse
click passed straight through it. The same rule's grid styles were also
quietly mangling the button's layout.
**Fix:** The overlay class is renamed `pencil-marks`, with a comment at the
rule explaining why the obvious name is dangerous.
**Lesson:** Generic class names are collisions waiting to happen, even in
scoped CSS - isolation namespaces per component, not per element role
within a component. Name classes for what they style, not for the feature
they belong to.

### Mojibake from a Windows-1252 save
**Bug:** The About page showed `9?9`, `3?3` and `complexity?easy` (each `?` a
U+FFFD replacement glyph). The file had been saved as Windows-1252 but is
served as UTF-8, so its `0xD7` (`x`) and `0x97` (em dash) bytes were invalid
sequences. The Japanese text had already degraded to literal `?` in an earlier
save.
**Found:** Reading the file, then confirming at byte level - a histogram showed
exactly three `0xD7` and two `0x97` and no other non-ASCII bytes in the file.
**Fix:** Decoded as cp1252, restored the multiplication signs, em dashes and
Japanese, re-saved as UTF-8 with the original CRLF endings.
**Verified:** Rendered in a browser - `9x9 grid` and `3x3 subgrids` with real
multiplication signs.
**Lesson:** An editor that quietly saves in the system codepage produces a file
that compiles, passes every test, and is wrong only to the reader. Check
encoding at the byte level - `?` and the replacement glyph are data loss, not
a display quirk.

### The flex item that refused to shrink
**Bug:** The About page overflowed sideways: a page-level horizontal scrollbar,
with body text clipped mid-sentence at the right edge.
**Found:** Measured in the live page - `documentElement.scrollWidth` 1384
against `clientWidth` 1272, so 112px over. `main` is a flex item, and flex
items default to `min-width:auto`, which refuses to shrink below the content's
intrinsic width; the five-item timeline (5 x 180px plus gaps) set that floor.
**Fix:** `min-width:0` on `main`.
**Verified before writing it:** setting `main.style.minWidth='0'` in the
running DOM dropped `scrollWidth` to exactly `clientWidth`, while
`.timeline-section` kept scrolling inside its own `overflow-x:auto`. The edit
was made only after the measurement agreed.
**Lesson:** `min-width:auto` is the default that surprises people - an inner
`overflow-x:auto` cannot rescue you if the flex ancestor never shrinks. And a
one-line probe in the live DOM turns a CSS guess into a measurement.

### The overlay that could never line up
**Bug:** The selected 3x3 block was highlighted by an absolutely-positioned
box, and it sat a few pixels off the block it was meant to cover.
**Found:** Measured the real grid instead of trusting the arithmetic.
`box-sizing` is `content-box` here, so a cell occupies 54px - except cells
carrying a 4px 3x3 separator, which occupy 57px. The pitch is not uniform. The
code assumed a flat 52px plus one border per row and produced 159px with a
158px square, where the block actually starts at 163.7px and spans ~164px.
Fractional device-pixel ratios shift it again.
**Fix:** Deleted the overlay. The component already computed a `blockhl` class
for every cell in the selected block, so the highlight is drawn on the cells
themselves.
**Lesson:** If a highlight has to line up with elements, draw it *on* those
elements. A parallel pixel model is a second source of truth, and it drifts -
with zoom, with device-pixel ratio, and with the next border change.

### display:none sent the colour picker to the corner of the page
**Bug:** Clicking a colour chip opened the native colour picker in the top-left
corner of the page instead of beside the chip.
**Found:** The `<input type="color">` was hidden with `display:none`. An element
with `display:none` generates no box, so the browser has no anchor for the
popup and falls back to the viewport origin.
**Fix:** The input stays in the layout, stretched invisibly over the chip
(`position:absolute; inset:0; opacity:0`).
**Lesson:** Native popups anchor to their control's box. A control that still
has to open UI must be made invisible, never boxless.

### Streaming every frame of a native picker over the circuit
**Bug:** The colour chip used `@oninput`, which fires continuously while the
native picker is dragged - every frame became a round-trip. The hue wheels
beside it were already rate-limited; the chip was not.
**Fix:** `@oninput` is throttled to 40ms like the wheels, with `@onchange`
committing the final value unthrottled so the chosen colour is never dropped.
**Lesson:** `oninput` on a native picker is a stream, not an event. Throttle
the stream, but keep an unthrottled commit or you lose the value the user
actually picked.

### A drag that never ended on touch
**Bug:** Drag-select across cells was ended by `mouseup` and `mouseleave` only,
so on a touch device the drag could stay armed after the finger lifted.
**Fix:** The board also ends the drag on `pointerup` and `pointercancel`.
**Lesson:** If an interaction starts from pointer events, end it from pointer
events. Mouse events are not guaranteed to follow on touch.

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

## Testing discipline

### The flaky test that was the test's fault
**Bug:** A test assumed the first empty cell's row contained a second empty
cell; on a 41-clue board that is occasionally false (~1 in 10 runs failed).
**Fix:** Scan for a row that actually satisfies the precondition.
**Lesson:** Test preconditions are claims about generated data - either
guarantee them or search for them, never assume them.

### Keep a slow oracle around
**Practice:** When the solver was rewritten for speed (bitmasks +
most-constrained-cell, ~45x faster generation), the old naive solution
counter was kept in the test suite and an agreement test added.
**Lesson:** An independent, obviously-correct implementation is the
cheapest insurance an optimization can buy.

### Synthetic events are not real input
**Bug (in the verification, not the app):** The click-transparent Notes
button above shipped as "browser-verified" - because the verification
dispatched synthetic events (`element.click()`, constructed
`PointerEvent`s) directly at the element. Dispatched events skip hit
testing entirely, so `pointer-events:none` never got exercised: the checks
passed against a button no real mouse could reach. The bug was only
reproduced when the flow was driven through real input.
**Fix / practice:** UI verification must include the real input pipeline -
actual clicks at coordinates resolved through the compositor - for at
least the happy path. Synthetic dispatch remains useful for fast state
checks, but it verifies handlers, not reachability.
**Lesson:** A test that bypasses the layer where the bug lives will pass
forever. Hit testing, focus, overlays and z-order only exist for real
input - and a user saying "it doesn't work" outranks a green check that
never touched what the user touches.

### Silent no-ops read as missing features
**Bug (UX):** Notes mode ignored digit presses whenever no cell - or a
filled or given cell - was selected, correctly but silently. To a player,
a feature that gives zero feedback on every attempt is indistinguishable
from one that was never built, and it was reported exactly that way.
**Fix:** Every formerly-silent rejection now explains itself (what
happened, and what to do instead), and toggling notes mode announces how
to use it.
**Lesson:** Correct-but-silent is a bug in the user's model even when the
state machine is right. If an action is refused, say so.

### Verify in the medium the bug lives in
**Practice:** Logic claims are proven by unit tests; rendering and
interaction claims (ARIA wiring, keyboard navigation, persistence across a
real page reload) were verified in a browser before each commit that
touched them.
**Lesson:** Match the verification to the failure mode - and then, where
possible, promote the manual check into an automated one (that is how the
smoke test and the `GameSession` unit tests came to exist).

### Reading the DOM before the round-trip landed
**Bug (in the verification, not the app):** The colour wheels were reported
broken - three real clicks on the ring, and the hex value did not change once.
That conclusion was wrong. In Blazor Server every interaction is a network
round-trip: the event goes to the server, the handler runs, and the DOM diff
comes back over the WebSocket. The value was read in the same batch as the
click, before any of that had happened, so it measured the pre-click DOM. Read
a moment later it was `#00fffd` - exactly the cyan that ring position should
produce. The feature had worked every time.
**Fix / practice:** An assertion after an interaction has to wait for the
update to arrive, not merely for the click to be dispatched.
**Lesson:** Cause and effect are not simultaneous in a server-rendered UI. An
assertion that races the transport will report a working feature as broken -
so "I clicked it and nothing happened" is a claim about timing until proven
otherwise. Reporting a bug that does not exist costs the same trust as missing
one that does.

### Screenshot pixels are not CSS pixels
**Bug (in the verification):** Several clicks aimed from a screenshot missed
entirely - one landed in a wheel's dead centre hole, another selected text
instead of grabbing a control. The display ran at `devicePixelRatio` 1.128, so
a 1456px-wide screenshot mapped onto a 1291px CSS viewport, and one target was
below the fold entirely.
**Fix / practice:** Take coordinates from `getBoundingClientRect()`, scroll the
element into view first, and confirm the element actually received the event -
a temporary listener reporting `isTrusted` and the exact offsets settles it in
one step.
**Lesson:** Reading coordinates off a scaled screenshot is guessing. When a UI
check fails, first prove the input landed where you think it did, or you will
debug the application for a defect in the harness.

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

See also: [architecture review](architecture-review.md) ·
[testing](testing.md)

[← Back to main README](../README.md)
