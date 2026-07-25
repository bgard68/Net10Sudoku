using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Application.Services;

// Coordinates the use-cases the UI invokes. Owns the per-player game state
// (board, selection, notes mode, mistakes); undo/redo bookkeeping lives in
// BoardHistory so this class stays a coordinator rather than a data structure.
public sealed class SudokuService : IGameService
{
    private readonly ISudokuGenerator _generator;
    private readonly ISudokuSolver _solver;
    private readonly ISudokuValidator _validator;
    private readonly IConflictDetector _conflicts;
    private readonly BoardHistory _history = new();

    public Board Current { get; private set; } = new();
    public Position? Selected { get; private set; }

    // When on, number entry toggles pencil marks instead of placing values.
    public bool NotesMode { get; private set; }

    // Wrong placements this game. Undo deliberately does not forgive them -
    // the mistake happened, taking it back doesn't unhappen it.
    public int Mistakes { get; private set; }

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    public SudokuService(ISudokuGenerator generator, ISudokuSolver solver, ISudokuValidator validator, IConflictDetector conflicts)
    {
        _generator = generator;
        _solver = solver;
        _validator = validator;
        _conflicts = conflicts;
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
        Mistakes = 0;
        _history.Clear();
    }

    // Adopt a board restored from persisted state (e.g. after a page refresh).
    // History intentionally starts empty - undo cannot reach past the reload.
    public void Restore(Board board, int mistakes = 0)
    {
        Current = board;
        StartFresh();
        Mistakes = Math.Max(0, mistakes);
    }

    public void ClearSelection() => Selected = null;

    public void Select(int row, int col) => Selected = new Position(row, col);

    public void ToggleNotesMode() => NotesMode = !NotesMode;

    public void Place(int value)
    {
        if (Selected is null) return;
        var (r, c) = Selected.Value;
        var cell = Current[r, c];
        if (cell.IsGiven) return;

        if (NotesMode)
        {
            if (cell.Value is not null) return;
            _history.Record(Current);
            Current.ToggleNote(r, c, value);
            return;
        }

        if (cell.Value == value) return;
        _history.Record(Current);
        Current.Set(r, c, value);
        RemoveNoteFromPeers(r, c, value);

        // Only judged when the answer is known; hand-built boards without a
        // recorded solution never count mistakes.
        if (Current.SolutionAt(r, c) is int correct && value != correct)
            Mistakes++;
    }

    public void Clear()
    {
        if (Selected is null) return;
        var (r, c) = Selected.Value;
        var cell = Current[r, c];
        if (cell.IsGiven) return;
        if (cell.Value is null && cell.Notes.Count == 0) return;

        _history.Record(Current);
        Current.Set(r, c, null);
        Current.ClearNotes(r, c);
    }

    public void ClearAll()
    {
        bool anything = false;
        for (int r = 0; r < 9 && !anything; r++)
        for (int c = 0; c < 9 && !anything; c++)
        {
            var cell = Current[r, c];
            if (!cell.IsGiven && (cell.Value is not null || cell.Notes.Count > 0))
                anything = true;
        }
        if (!anything) return;

        _history.Record(Current);
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (Current[r, c].IsGiven) continue;
            Current.Set(r, c, null);
            Current.ClearNotes(r, c);
        }
    }

    public void Undo()
    {
        if (_history.Undo(Current) is Board previous)
            Current = previous;
    }

    public void Redo()
    {
        if (_history.Redo(Current) is Board next)
            Current = next;
    }

    public bool Validate() => _validator.IsValid(Current);
    public bool IsComplete() => _validator.IsComplete(Current);

    public bool Solve()
    {
        // Fill from the recorded solution when we have one. Solving the live board
        // would fail as soon as the player has entered a wrong value.
        if (Current.HasSolution)
        {
            _history.Record(Current);
            for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                var v = Current.SolutionAt(r, c);
                if (v is > 0 && !Current[r, c].IsGiven)
                    Current.Set(r, c, v.Value);
            }
            return true;
        }

        var copy = GivensOnly(Current);
        var ok = _solver.TrySolve(copy);
        if (ok)
        {
            _history.Record(Current);
            Current = copy;
            return true;
        }
        return false;
    }

    public (Position pos, int value)? GetHintForSelectedCell()
    {
        if (Selected is null) return null;

        var (r, c) = Selected.Value;

        // Can't provide hint for given cells
        if (Current[r, c].IsGiven) return null;

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
            if (board[r, c].IsGiven)
                bare.Set(r, c, board.Get(r, c), given: true);
        }
        return bare;
    }

    public void ApplyHintForSelectedCell()
    {
        var hint = GetHintForSelectedCell();
        if (hint is null) return;
        var (pos, value) = hint.Value;
        // Do not overwrite given cells
        if (Current[pos.Row, pos.Col].IsGiven) return;
        _history.Record(Current);
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
        for (int c = 0; c < 9; c++) Current.RemoveNote(row, c, value);
        for (int r = 0; r < 9; r++) Current.RemoveNote(r, col, value);
        int br = row / 3 * 3, bc = col / 3 * 3;
        for (int r = br; r < br + 3; r++)
        for (int c = bc; c < bc + 3; c++)
            Current.RemoveNote(r, c, value);
    }
}
