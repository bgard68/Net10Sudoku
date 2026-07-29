[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)

# Lessons: testing discipline

What the test suite got wrong about itself, and the verification habits that came out of it.

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


[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)
