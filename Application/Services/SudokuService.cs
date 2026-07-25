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

    // Undo works on full-board snapshots, so any action - a placement that swept
    // peers' notes included - reverts atomically with one pop.
    private readonly Stack<Board> _undo = new();
    private readonly Stack<Board> _redo = new();

    public Board Current { get => _state.Current; private set => _state.Current = value; }
    public Position? Selected { get => _state.Selected; private set => _state.Selected = value; }

    // When on, number entry toggles pencil marks instead of placing values.
    public bool NotesMode { get; private set; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

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
        StartFresh();
    }

    // Generation grades candidates until one lands in the difficulty band, which
    // can take a moment - run it off the caller's thread so a Blazor circuit
    // stays responsive while it works.
    public async Task NewAsync(Difficulty difficulty)
    {
        var board = await Task.Run(() => _generator.Generate(difficulty));
        Current = board;
        StartFresh();
    }

    private void StartFresh()
    {
        Selected = null;
        _undo.Clear();
        _redo.Clear();
    }

    // Adopt a board restored from persisted state (e.g. after a page refresh).
    // History intentionally starts empty - undo cannot reach past the reload.
    public void Restore(Board board)
    {
        Current = board;
        StartFresh();
    }

    public void ClearSelection() => Selected = null;

    public void Select(int row, int col) => Selected = new Position(row, col);

    public void ToggleNotesMode() => NotesMode = !NotesMode;

    public void Place(int value)
    {
        if (Selected is null) return;
        var (r, c) = Selected.Value;
        var cell = Current.Cells[r, c];
        if (cell.IsGiven) return;

        if (NotesMode)
        {
            if (cell.Value is not null) return;
            Snapshot();
            cell.ToggleNote(value);
            return;
        }

        if (cell.Value == value) return;
        Snapshot();
        cell.Set(value);
        RemoveNoteFromPeers(r, c, value);
    }

    public void Clear()
    {
        if (Selected is null) return;
        var (r, c) = Selected.Value;
        var cell = Current.Cells[r, c];
        if (cell.IsGiven) return;
        if (cell.Value is null && cell.Notes.Count == 0) return;

        Snapshot();
        cell.Set(null);
        cell.ClearNotes();
    }

    public void ClearAll()
    {
        bool anything = false;
        for (int r = 0; r < 9 && !anything; r++)
        for (int c = 0; c < 9 && !anything; c++)
        {
            var cell = Current.Cells[r, c];
            if (!cell.IsGiven && (cell.Value is not null || cell.Notes.Count > 0))
                anything = true;
        }
        if (!anything) return;

        Snapshot();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var cell = Current.Cells[r, c];
            if (cell.IsGiven) continue;
            cell.Set(null);
            cell.ClearNotes();
        }
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Current.Clone());
        Current = _undo.Pop();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(Current.Clone());
        Current = _redo.Pop();
    }

    private void Snapshot()
    {
        _undo.Push(Current.Clone());
        _redo.Clear();
    }

    public bool Validate() => _validator.IsValid(Current);
    public bool IsComplete() => _validator.IsComplete(Current);

    public bool Solve()
    {
        // Fill from the recorded solution when we have one. Solving the live board
        // would fail as soon as the player has entered a wrong value.
        if (Current.HasSolution)
        {
            Snapshot();
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
            Snapshot();
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
        Snapshot();
        Current.Set(pos.Row, pos.Col, value);
        RemoveNoteFromPeers(pos.Row, pos.Col, value);
    }

    public void ApplyHintForSelectedCell()
    {
        var hint = GetHintForSelectedCell();
        if (hint is null) return;
        var (pos, value) = hint.Value;
        // Do not overwrite given cells
        var targetCell = Current.Cells[pos.Row, pos.Col];
        if (targetCell.IsGiven) return;
        Snapshot();
        Current.Set(pos.Row, pos.Col, value);
        RemoveNoteFromPeers(pos.Row, pos.Col, value);
    }

    public bool HasConflict(int row, int col)
    {
        return _conflicts.HasConflict(Current, row, col);
    }

    // Placing a value makes that digit impossible for every peer, so tidy away the
    // now-stale pencil marks the way a human eraser would.
    private void RemoveNoteFromPeers(int row, int col, int value)
    {
        for (int c = 0; c < 9; c++) Current.Cells[row, c].RemoveNote(value);
        for (int r = 0; r < 9; r++) Current.Cells[r, col].RemoveNote(value);
        int br = row / 3 * 3, bc = col / 3 * 3;
        for (int r = br; r < br + 3; r++)
        for (int c = bc; c < bc + 3; c++)
            Current.Cells[r, c].RemoveNote(value);
    }
}
