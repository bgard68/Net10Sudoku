[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)

# Lessons: game correctness

Defects in the rules, the solver and the game's own state - the bugs that made the app give a wrong answer rather than look wrong.

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
until the grade lands in the band ([details](../puzzle-generation.md)).
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


[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)
