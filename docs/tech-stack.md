[← Back to main README](../README.md)

# Tech stack

| Layer | Choice | Why |
|---|---|---|
| Runtime | **.NET 10** (SDK pinned by feature band in `global.json`) | Current LTS-track platform; `TimeProvider`, collection expressions, and modern C# throughout |
| UI | **Blazor Server** (interactive server render mode) | One language end to end; server-side state fits a game whose rules live in C#; no API surface to secure or version |
| Real-time transport | **SignalR** (built into Blazor Server) | UI events and renders travel over the circuit; the smoke test exercises its negotiate endpoint directly |
| State persistence | **`ProtectedLocalStorage`** (ASP.NET Data Protection) | Survives refreshes without a database; payloads are encrypted and integrity-checked, and restored snapshots are validated as untrusted input anyway |
| Styling | **CSS isolation** (scoped `.razor.css`, `::deep` for child components) | Component styles stay with components; no global stylesheet drift |
| Domain/logic | **Plain C#**, zero packages | The solver, generator and grader are self-contained; no external Sudoku or math libraries |
| Tests | **xUnit** + coverlet | 87 tests over the real service graph; `TimeProvider` fakes for clock control, in-memory `IGameStore` for persistence |
| Smoke test | **PowerShell** (5.1 and 7+ compatible) | One script runs on a developer Windows box and the Ubuntu CI runner; no extra tooling to install |
| CI | **GitHub Actions** (two jobs: build+test, smoke) | Least-privilege token (`contents: read`); every push and PR gated |
| Build hygiene | `Directory.Build.props` | Shared TFM/nullable/implicit-usings and `TreatWarningsAsErrors` in one place |
| Repo hygiene | `.gitattributes` (`* text=auto`) | LF-normalized repository content, byte-stable diffs across platforms |

## Notable implementation techniques

- **Bitmask solving.** Row/column/box candidate masks make a candidate check
  three reads; most-constrained-cell-first branching prunes the search tree.
  Generation medians sit at 0.4-16 ms *including* difficulty-band retries
  ([numbers](puzzle-generation.md)).
- **Strategy-pattern grading.** Each human technique is a class over a shared
  candidate-grid model; the grader orders them by tier and records the
  dearest one needed ([techniques](solving-techniques.md)).
- **Ports and adapters.** Application-layer ports (`IGameStore`,
  `ISudokuSolver`, ...) with adapters in outer layers; unit tests plug fakes
  into the same sockets ([architecture](architecture.md)).
- **Snapshot-based undo.** Full-board clones per action make compound
  mutations revert atomically at negligible cost for an 81-cell board.
- **O(81) render projection.** One pass over the board per render derives
  conflict highlighting and number-pad completion for every cell.

## Dependencies

Deliberately minimal:

- `Microsoft.Extensions.DependencyInjection.Abstractions` (class libraries'
  DI registration helpers)
- Test-only: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
  `coverlet.collector`

Everything else - puzzle logic, grading, persistence mapping - is
first-party code.

[← Back to main README](../README.md)
