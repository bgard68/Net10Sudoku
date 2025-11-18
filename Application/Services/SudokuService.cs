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
    private readonly IHintOrchestrator _hintOrchestrator;

    public Board Current { get => _state.Current; private set => _state.Current = value; }
    public Position? Selected { get => _state.Selected; private set => _state.Selected = value; }

    public SudokuService(ISudokuGenerator generator, ISudokuSolver solver, ISudokuValidator validator, ISudokuHintProvider hints, IConflictDetector conflicts, IGameState state, IHintOrchestrator hintOrchestrator)
    {
        _generator = generator;
        _solver = solver;
        _validator = validator;
        _hints = hints;
        _conflicts = conflicts;
        _state = state;
        _hintOrchestrator = hintOrchestrator;
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
        var copy = Current.Clone();
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
        
        // If cell already has a value, we need to solve to find the correct one
        // Create a copy of the board and solve it to get the correct answer
        var copy = Current.Clone();
        if (_solver.TrySolve(copy))
        {
            var correctValue = copy.Get(r, c);
            if (correctValue.HasValue)
            {
                return (new Position(r, c), correctValue.Value);
            }
        }
        
        return null;
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
