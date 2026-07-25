using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Application.Services;

public sealed class SudokuService
{
    private readonly ISudokuGenerator _generator;
    private readonly ISudokuSolver _solver;
    private readonly ISudokuValidator _validator;
    private readonly ISudokuHintProvider _hints;
    private readonly IConflictDetector _conflicts;
    private readonly IGameState _state;

    public Board Current { get => _state.Current; private set => _state.Current = value; }
    public Position? Selected { get => _state.Selected; private set => _state.Selected = value; }

    public SudokuService(ISudokuGenerator generator, ISudokuSolver solver, ISudokuValidator validator, ISudokuHintProvider hints, IConflictDetector conflicts, IGameState state)
    {
        _generator = generator;
        _solver = solver;
        _validator = validator;
        _hints = hints;
        _conflicts = conflicts;
        _state = state;
    }

    public void New(Difficulty difficulty)
    {
        Current = _generator.Generate(difficulty);
        Selected = null;
    }

    public void ClearSelection() => Selected = null;

    public void Select(int row, int col) => Selected = new Position(row, col);

    public void Place(int value)
    {
        if (Selected is null) return;
        var (r,c) = Selected.Value;
        var cell = Current.Cells[r,c];
        if (cell.IsGiven) return;
        cell.Set(value);
    }

    public void Clear()
    {
        if (Selected is null) return;
        var (r,c) = Selected.Value;
        var cell = Current.Cells[r,c];
        if (cell.IsGiven) return;
        cell.Set(null);
    }

    public void ClearAll()
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var cell = Current.Cells[r, c];
            if (!cell.IsGiven)
                cell.Set(null);
        }
    }

    public bool Validate() => _validator.IsValid(Current);
    public bool IsComplete() => _validator.IsComplete(Current);

    public bool Solve()
    {
        // Fill from the recorded solution when we have one. Solving the live board
        // would fail as soon as the player has entered a wrong value.
        if (Current.HasSolution)
        {
            for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                var v = Current.SolutionAt(r, c);
                if (v is > 0 && !Current.Cells[r, c].IsGiven)
                    Current.Set(r, c, v.Value);
            }
            return true;
        }

        var copy = GivensOnly(Current);
        var ok = _solver.TrySolve(copy);
        if (ok)
        {
            Current = copy;
            return true;
        }
        return false;
    }

    public (Position pos, int value)? Hint() => _hints.GetNextHint(Current);

    public (Position pos, int value)? GetHintForSelectedCell()
    {
        if (Selected is null) return null;

        var (r, c) = Selected.Value;
        var cell = Current.Cells[r, c];

        // Can't provide hint for given cells
        if (cell.IsGiven) return null;

        // Prefer the solution captured at generation time. Solving the live board
        // instead would fail outright once the player has entered a wrong value
        // anywhere, and would only ever echo back whatever is already in this cell.
        var known = Current.SolutionAt(r, c);
        if (known is > 0) return (new Position(r, c), known.Value);

        // Fallback for boards with no recorded solution (e.g. one built by hand in a
        // test): solve from the givens only, ignoring the player's entries.
        var fromGivens = GivensOnly(Current);
        if (_solver.TrySolve(fromGivens))
        {
            var correctValue = fromGivens.Get(r, c);
            if (correctValue.HasValue)
                return (new Position(r, c), correctValue.Value);
        }

        return null;
    }

    // A copy holding only the puzzle's clues, so solving is unaffected by user entries.
    private static Board GivensOnly(Board board)
    {
        var bare = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (board.Cells[r, c].IsGiven)
                bare.Cells[r, c].Set(board.Get(r, c), given: true);
        }
        return bare;
    }

    public void ApplyHint()
    {
        var h = _hints.GetNextHint(Current);
        if (h is null) return;
        var (pos, value) = h.Value;
        // Do not overwrite given cells
        var targetCell = Current.Cells[pos.Row, pos.Col];
        if (targetCell.IsGiven) return;
        Current.Set(pos.Row, pos.Col, value);
    }

    public void ApplyHintForSelectedCell()
    {
        var hint = GetHintForSelectedCell();
        if (hint is null) return;
        var (pos, value) = hint.Value;
        // Do not overwrite given cells
        var targetCell = Current.Cells[pos.Row, pos.Col];
        if (targetCell.IsGiven) return;
        Current.Set(pos.Row, pos.Col, value);
    }



    public bool HasConflict(int row, int col)
    {
        return _conflicts.HasConflict(Current, row, col);
    }

}
