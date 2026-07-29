[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)

# Lessons: frontend and rendering

CSS, layout, input handling and the browser's own behaviour - the largest cluster, because this is where 'looks fine' and 'works' diverge most often.

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

### Controls that looked fine and measured under AA
**Bug:** Two controls shipped below the WCAG AA 4.5:1 minimum: the filled
active difficulty pill (white on `#16a34a`, **3.3:1**) and the quiet action
links (`#64748b`, **3.99:1**). Both were my own colour choices from the UI
redesign, and both look perfectly legible - "it reads fine to me" is exactly
how sub-AA contrast survives review.
**Fix:** Darkened all four active-pill fills to their 700-weight shades and
lifted the link colour to `#94a3b8`. Re-measured across every difficulty state
(only the selected pill is filled, so each state needed its own check) and with
the colour panel open: zero failures.
**Lesson:** Contrast is a measurement, not an opinion - and the *active* state
of a control is a different measurement from its resting state. Check every
variant a control can be in, not the one that happens to be on screen.

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


[← Lessons index](../lessons-learned.md) · [← Main README](../../README.md)
