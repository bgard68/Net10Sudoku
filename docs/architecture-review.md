[← Back to main README](../README.md)

# Architecture review: findings, fixes, and deliberate non-fixes

A structured review of the codebase was carried out against SOLID and Clean
Architecture principles, with every finding verified empirically (measured or
reproduced) before being treated as real. This document records what was
found, what was done about it, and - just as importantly - what was
deliberately *not* done and why. The full change history is in the git log;
each fix landed as its own commit with the reasoning in the message.

## Original findings and their fixes

### Correctness defects

| Finding | Evidence | Fix |
|---|---|---|
| **One wrong entry disabled hints board-wide.** Hints were derived by re-solving the live board, wrong entries included; an incorrect digit anywhere made the board unsolvable. | Measured: after one wrong digit, hints worked on 0 of 39 empty cells. | The generator records the completed grid before digging; hints, Solve and mistake-counting read that recorded solution in O(1). |
| **Difficulty was clue count, not difficulty.** Removal count was the only difficulty lever. | Measured: half of "Hard" boards were solvable with singles alone; "Professional" was statistically no harder than Hard. | Technique-based grading: every candidate board is graded by the techniques it demands, and generation retries until the grade lands in the band. See [puzzle generation](puzzle-generation.md). |
| **Theme was shared across all users.** `ThemeService` was registered as a singleton in a Blazor Server app, where singletons span every circuit. | One visitor toggling dark mode changed it for everyone. | Registered scoped (per circuit). |
| **Broken overlay CSS and pathological win animation.** A stray brace produced invalid inline CSS; fireworks randomized inside the render loop, rebuilding 500 elements on every diff. | Read directly from the markup. | Interpolation fixed; burst layout generated once per win. |

### SOLID and Clean Architecture findings

| Principle | Finding | Fix |
|---|---|---|
| **Encapsulation / CA** | `Board` exposed its raw `Cell[,]` and `Cell` had public mutators - any layer could bypass the game rules, or replace a cell object outright. The given-cell invariant held by convention only. | Cell mutators are `internal`, the array is private; reads via a read-only indexer, writes only through `Board` methods. The invariant is now enforced by the compiler. |
| **SRP** | The game page was a ~600-line component mixing input, persistence, timing, view derivation and celebration effects. The service carried undo stacks inline. | Page split into `NumberPad`, `GameStatusBar`, `WinBanner`, `Celebration`; undo/redo extracted to `BoardHistory`; game flow extracted to `GameSession`. |
| **OCP** | Grading techniques were private methods inside the grader - extending it meant editing the algorithm. | Techniques are `IGradingTechnique` strategies over a shared candidate grid; adding one is a new class plus a DI registration. |
| **DIP** | The UI depended on the concrete `SudokuService`; game-flow persistence depended on a concrete storage class. | UI depends on `IGameService`; `GameSession` depends on the `IGameStore` port (browser-storage adapter in the host) and `TimeProvider`. Both are faked in unit tests. |
| **Dead abstractions** | A hint-provider interface and an orchestrator were registered in DI and injected but had no reachable caller, duplicating logic the grader already owned. | Deleted. A registered-but-unreachable API is worse than no API. |
| **State ownership** | An `IGameState` abstraction held two properties while the rest of the game state lived elsewhere - it described half the state it claimed to own. | Removed; the service owns all of its state, history delegated to `BoardHistory`. |

### Performance findings

| Finding | Evidence | Fix |
|---|---|---|
| Naive solver: 27 cell scans per candidate check, first-empty-cell branching. | Professional generation: 266 ms median, 543 ms worst - on the UI thread. | Bitmask solver with most-constrained-cell ordering; generation medians dropped to 0.4-16 ms including band-grading retries, and generation moved off the circuit thread. |
| Per-render board scans: conflict checks and digit-completion recomputed per cell, ~5,000 reads per render. | Read directly from the render path. | One O(81) pass per render feeds both. |

## What was deliberately NOT done - and why

These were considered and rejected. The test applied to each: *does anything
become possible, safer, or testable that was not before?* If the answer is
no, the change is ceremony - it alters a checklist, not the software.

| Candidate change | Why it was rejected |
|---|---|
| **An interface for `ThemeService`** | It was a bool with an event, consumed only by layout components in the same project. No test wanted to fake it, no boundary crossed it, no second implementation was conceivable. The theme system was later removed entirely in favour of a single dark theme - which retired the question, and vindicated not having spent the abstraction on it. |
| **Moving per-layer DI registration (`AddApplication()` / `AddInfrastructure()`) into the host** | Satisfies a strict reading of doctrine at the cost of scattering each layer's registration knowledge away from the layer that owns it. Zero change to behavior, testability or coupling in practice; the current pattern is a deliberate, widely used convention. |
| **Splitting `IGameService` into role interfaces** | Interface segregation protects consumers from members they do not use; this interface has one consumer that uses nearly all of it. Splitting would add files, not safety. |

A note on the one interface that *was* added late: `IGameStore` exists
because the `GameSession` unit tests demanded an in-memory fake - the
abstraction arrived when something needed it, which is exactly the standard
the rejected items failed.

## Resulting assessment

| Area | State |
|---|---|
| Clean Architecture | Dependency rule verified from project references; domain invariants compiler-enforced; flow logic in the Application layer behind ports; storage as an adapter. |
| SRP | Page is markup + event forwarding; flow, history, and each grading technique are single-purpose classes. |
| OCP | Grader extensible by registration, difficulty bands data-driven. |
| DIP | Every cross-boundary dependency sits behind an abstraction that has a real consumer. |
| Verification | 87 unit tests plus a 14-check HTTP smoke test, all green in CI on every push. |

See also: [architecture](architecture.md) · [lessons learned](lessons-learned.md)

[← Back to main README](../README.md)
