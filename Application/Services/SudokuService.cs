using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Application.Services;

public sealed class SudokuService
{
    private readonly ISudokuGenerator _generator;
    private readonly ISudokuSolver _solver;
    private readonly ISudokuValidator _validator;
    private readonly ISudokuHintProvider _hints;

    public Board Current { get; private set; } = new();
    public Position? Selected { get; private set; }

    public SudokuService(ISudokuGenerator generator, ISudokuSolver solver, ISudokuValidator validator, ISudokuHintProvider hints)
    {
        _generator = generator;
        _solver = solver;
        _validator = validator;
        _hints = hints;
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
        Current.Set(pos.Row, pos.Col, value);
    }

    public void ApplyHintForSelectedCell()
    {
        var hint = GetHintForSelectedCell();
        if (hint is null) return;
        var (pos, value) = hint.Value;
        Current.Set(pos.Row, pos.Col, value);
    }

    public List<Position> GetConflictingCells(int row, int col)
    {
        var conflicts = new List<Position>();
        var value = Current.Get(row, col);
        
        if (value is null) return conflicts;

        // Check row for conflicts
        for (int c = 0; c < 9; c++)
        {
            if (c != col && Current.Get(row, c) == value)
            {
                conflicts.Add(new Position(row, c));
            }
        }

        // Check column for conflicts
        for (int r = 0; r < 9; r++)
        {
            if (r != row && Current.Get(r, col) == value)
            {
                conflicts.Add(new Position(r, col));
            }
        }

        // Check 3x3 box for conflicts
        int boxRow = (row / 3) * 3;
        int boxCol = (col / 3) * 3;
        for (int r = boxRow; r < boxRow + 3; r++)
        {
            for (int c = boxCol; c < boxCol + 3; c++)
            {
                if ((r != row || c != col) && Current.Get(r, c) == value)
                {
                    conflicts.Add(new Position(r, c));
                }
            }
        }

        return conflicts;
    }

    public bool HasConflict(int row, int col)
    {
        var value = Current.Get(row, col);
        if (value is null) return false;
        return !_validator.CanPlace(Current, row, col, value.Value);
    }

}
