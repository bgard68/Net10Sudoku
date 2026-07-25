namespace Sudoku.Domain;

// The 9x9 grid. Cells are exposed read-only through the indexer; every mutation
// goes through a Board method so the given-cell invariant is enforced in one
// place and callers can never swap a cell out of the grid.
public sealed class Board
{
    private readonly Cell[,] _cells;
    private int[,]? _solution;

    public Board()
    {
        _cells = new Cell[9,9];
        for (var r = 0; r < 9; r++)
        for (var c = 0; c < 9; c++)
            _cells[r,c] = new Cell(r,c);
    }

    // Read-only access to a cell; the cell's own mutators are internal.
    public Cell this[int row, int col] => _cells[row, col];

    public int? Get(int r, int c) => _cells[r,c].Value;

    public void Set(int r, int c, int? v, bool given = false) => _cells[r,c].Set(v, given);

    public void ToggleNote(int r, int c, int value) => _cells[r,c].ToggleNote(value);

    public void RemoveNote(int r, int c, int value) => _cells[r,c].RemoveNote(value);

    public void ClearNotes(int r, int c) => _cells[r,c].ClearNotes();

    // True when the puzzle's unique solution is known (set by the generator).
    public bool HasSolution => _solution is not null;

    // Record the puzzle's unique solution. Captured once at generation time so that
    // hints and answer checks never have to re-solve a board the player may have
    // filled in incorrectly.
    public void SetSolution(int[,] solution)
    {
        ArgumentNullException.ThrowIfNull(solution);
        if (solution.GetLength(0) != 9 || solution.GetLength(1) != 9)
            throw new ArgumentException("Solution must be a 9x9 grid.", nameof(solution));

        var copy = new int[9,9];
        Array.Copy(solution, copy, solution.Length);
        _solution = copy;
    }

    // The solved value for a cell, or null when no solution has been recorded.
    public int? SolutionAt(int r, int c) => _solution?[r,c];

    // Create a deep copy of the board (values, given flags, notes and any known solution)
    public Board Clone()
    {
        var copy = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var source = _cells[r,c];
            copy._cells[r,c].Set(source.Value, source.IsGiven);
            foreach (var note in source.Notes)
                copy._cells[r,c].ToggleNote(note);
        }
        if (_solution is not null) copy.SetSolution(_solution);
        return copy;
    }
}
