namespace Sudoku.Domain;

public sealed class Board
{
    private int[,]? _solution;

    public Cell[,] Cells { get; }

    public Board()
    {
        Cells = new Cell[9,9];
        for (var r = 0; r < 9; r++)
        for (var c = 0; c < 9; c++)
            Cells[r,c] = new Cell(r,c);
    }

    public int? Get(int r, int c) => Cells[r,c].Value;
    public void Set(int r, int c, int? v, bool given = false) => Cells[r,c].Set(v, given);

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

    // Create a deep copy of the board (values, given flags and any known solution)
    public Board Clone()
    {
        var copy = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var v = Cells[r,c].Value;
            var given = Cells[r,c].IsGiven;
            copy.Cells[r,c].Set(v, given);
        }
        if (_solution is not null) copy.SetSolution(_solution);
        return copy;
    }
}
