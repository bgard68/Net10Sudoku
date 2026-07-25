[← Back to main README](../README.md)

# Lessons learned

Every bug below actually happened in this project. Each entry records the
defect, the fix, and the transferable lesson. The full detail lives in the
git history - each fix landed as its own commit with reasoning in the
message.

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

### Verify in the medium the bug lives in
**Practice:** Logic claims are proven by unit tests; rendering and
interaction claims (ARIA wiring, keyboard navigation, persistence across a
real page reload) were verified in a browser before each commit that
touched them.
**Lesson:** Match the verification to the failure mode - and then, where
possible, promote the manual check into an automated one (that is how the
smoke test and the `GameSession` unit tests came to exist).

See also: [architecture review](architecture-review.md) ·
[testing](testing.md)

[← Back to main README](../README.md)
