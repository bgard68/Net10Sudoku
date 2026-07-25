using Sudoku.Domain;

namespace Sudoku.Application.Models;

// A serializable image of an in-progress game, used to survive page refreshes
// and circuit loss in Blazor Server. Kept in the Application layer so the
// mapping to and from the domain Board is unit-testable without any UI.
public sealed record GameSnapshot
{
    public required int[] Values { get; init; }    // 81 cells, 0 = empty
    public required bool[] Givens { get; init; }   // 81 flags
    public int[]? Solution { get; init; }          // 81 values, null if unknown
    public required int[] NoteMasks { get; init; } // 81 bitmasks, bit v = note v
    public int ElapsedSeconds { get; init; }
    public Difficulty Difficulty { get; init; }
    public int Mistakes { get; init; }

    public static GameSnapshot Capture(Board board, TimeSpan elapsed, Difficulty difficulty, int mistakes = 0)
    {
        var values = new int[81];
        var givens = new bool[81];
        var notes = new int[81];
        int[]? solution = board.HasSolution ? new int[81] : null;

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            int i = r * 9 + c;
            var cell = board.Cells[r, c];
            values[i] = cell.Value ?? 0;
            givens[i] = cell.IsGiven;
            foreach (var note in cell.Notes) notes[i] |= 1 << note;
            if (solution is not null) solution[i] = board.SolutionAt(r, c) ?? 0;
        }

        return new GameSnapshot
        {
            Values = values,
            Givens = givens,
            Solution = solution,
            NoteMasks = notes,
            ElapsedSeconds = (int)elapsed.TotalSeconds,
            Difficulty = difficulty,
            Mistakes = mistakes
        };
    }

    // Rebuilds a playable board. Throws on malformed data - callers treat any
    // failure as "no saved game" and fall back to generating a fresh puzzle,
    // because stored data is outside the application's control.
    public Board ToBoard()
    {
        if (Values.Length != 81 || Givens.Length != 81 || NoteMasks.Length != 81 ||
            (Solution is not null && Solution.Length != 81))
            throw new ArgumentException("Snapshot arrays must each hold 81 cells.");

        var board = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            int i = r * 9 + c;
            int v = Values[i];
            if (v is < 0 or > 9)
                throw new ArgumentException($"Snapshot value {v} at cell {i} is out of range.");

            if (v != 0) board.Cells[r, c].Set(v, Givens[i]);

            for (int note = 1; note <= 9; note++)
            {
                if ((NoteMasks[i] & (1 << note)) != 0)
                    board.Cells[r, c].ToggleNote(note);
            }
        }

        if (Solution is not null)
        {
            var grid = new int[9, 9];
            for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                int v = Solution[r * 9 + c];
                if (v is < 1 or > 9)
                    throw new ArgumentException($"Snapshot solution value {v} is out of range.");
                grid[r, c] = v;
            }
            board.SetSolution(grid);
        }

        return board;
    }
}
