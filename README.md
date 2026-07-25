# Sudoku - Blazor Server (.NET 10)

A Sudoku game built with Blazor Server on .NET 10, structured with Clean
Architecture. Every puzzle is verified to have exactly one solution, and
difficulty is graded by the solving techniques a puzzle actually requires -
not just by how many clues were removed.

## Features

### Gameplay
- **Four difficulty levels** - Easy, Medium, Hard, Professional
- **Technique-based difficulty** - each candidate puzzle is graded by replaying
  human solving techniques; generation retries until the puzzle genuinely
  demands its difficulty level (see [Difficulty grading](#difficulty-grading))
- **Unique solutions** - every removal during generation is verified to keep
  exactly one solution
- **Reliable hints** - hints come from the solution captured at generation
  time, so they keep working even after wrong entries, and can correct a cell
  you filled in wrongly
- **Pencil marks (notes)** - toggle Notes mode and jot candidate digits;
  placing a real value sweeps that digit's notes from the row, column and box
- **Undo / redo** - every action snapshots the whole board, so a placement
  that swept peers' notes reverts atomically
- **Timer and best times** - elapsed time per puzzle, with your fastest solve
  per difficulty remembered in the browser (auto-solve never sets records)
- **Mistake counter** - wrong entries are counted against the known solution;
  undo does not forgive them
- **Real-time conflict detection** - duplicates in a row, column or box are
  highlighted as you play
- **Validate / Solve / Clear All** - check progress, fill the solution, or
  start the puzzle over

### Input and accessibility
- **Keyboard-first grid** - the board is a single tab stop; arrow keys move
  the selection, digits 1-9 place values (or notes), Backspace/Delete clears,
  N toggles Notes mode
- **Screen-reader support** - proper `grid`/`row`/`gridcell` roles,
  `aria-activedescendant` tracking, and per-cell labels including value,
  given-status and notes
- **Mouse support** - click to select, drag to move the selection
- **Dark mode** - per-user theme toggle (each visitor gets their own)
- **Celebration fireworks** on a solved board

## Architecture

Clean Architecture with the dependency arrows pointing inward
(`Sudoku` -> `Infrastructure` -> `Application` -> `Domain`):

```
Sudoku.slnx
|-- Domain/            Core entities: Board (with recorded solution), Cell
|                      (value, given flag, notes), Position
|-- Application/       Interfaces (generator, solver, validator, grader,
|                      hints, conflicts, game state), Difficulty/TechniqueTier
|                      models, and SudokuService - the game coordinator
|-- Infrastructure/    Implementations: backtracking solver, uniqueness-
|                      verifying generator, technique-replaying grader,
|                      validator, conflict detector
|-- Sudoku/            Blazor Server host and UI components
`-- Sudoku.Tests/      xUnit suite covering domain, generation, grading,
                       hints, undo/redo and notes
```

All services are registered through `AddApplication()` / `AddInfrastructure()`
extension methods and injected via constructor injection.

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
  (the exact version is pinned in `global.json`)

### Run

```bash
dotnet run --project Sudoku
```

Then open `https://localhost:7086` or `http://localhost:5260`.

### Test

```bash
dotnet test
```

### Build

```bash
dotnet build
```

The build treats warnings as errors (see `Directory.Build.props`).

## How to play

1. Pick a difficulty to generate a new puzzle.
2. Select a cell (click, or Tab to the board and use the arrow keys).
3. Enter a digit with the keyboard or the number pad.
4. Toggle **Notes** (or press N) to pencil in candidates instead.
5. Use **Hint** for the selected cell, **Validate** to check for conflicts,
   **Undo**/**Redo** to step through your actions, and **Solve** to finish.

## Algorithm details

### Generation
1. Fill the three diagonal 3x3 boxes randomly (they cannot conflict).
2. Complete the grid with a backtracking solver.
3. Record the completed grid as the puzzle's solution.
4. Dig cells one at a time; every removal that would break uniqueness is
   reverted, so the board has exactly one solution at every step.
5. Grade the result by technique (below); if the grade is outside the
   requested difficulty band, carve a fresh candidate and try again.

### Difficulty grading

The grader replays human techniques from cheapest to dearest over a candidate
grid and reports the hardest tier it needed:

| Tier | Techniques |
|------|-----------|
| Singles | naked singles, hidden singles |
| LockedCandidate | pointing / claiming eliminations |
| Pair | naked pairs |
| Advanced | anything beyond the above (fish, chains, guessing) |

Difficulty bands map onto those tiers:

| Difficulty | Requirement | Clues removed |
|-----------|-------------|---------------|
| Easy | solvable with singles alone | 40 |
| Medium | needs a locked candidate or a pair | 50 |
| Hard | needs more than the cheap techniques | 55 |
| Professional | same requirement as Hard, fewer clues to work with | 60 |

### Hints
The hint for a cell is the value from the solution recorded at generation
time - an O(1) lookup that cannot be broken by wrong entries elsewhere on the
board. Boards constructed without a recorded solution (for example in tests)
fall back to solving from the given clues only.

## License

This project is open source under the [MIT License](LICENSE).

## Acknowledgments

- Leonhard Euler for Latin squares
- Howard Garns for inventing modern Sudoku
- Nikoli for popularizing it in Japan
