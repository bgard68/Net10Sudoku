using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Tests;

// Deterministic board fixtures. The canonical grid is a valid completed sudoku
// built from a shifted-row pattern, so tests that need "a known solution" run
// against a fixed, hand-verifiable board instead of the random generator.
internal static class TestBoards
{
    // The five cells the fixture puzzle leaves for the player. Chosen so the
    // holes cover one row pair, one column pair, one box cluster and one cell
    // unrelated to the others - and so every hole is a naked single, making the
    // puzzle deterministically solvable (and gradable) by the cheapest tier.
    public static readonly (int Row, int Col)[] PuzzleHoles =
        [(0, 0), (0, 1), (1, 0), (1, 1), (4, 4)];

    // Classic valid pattern: rows are shifts of 1..9 with distinct offsets per
    // row and per box band, so every row, column and box holds all nine digits.
    public static int CanonicalValueAt(int row, int col) =>
        (row * 3 + row / 3 + col) % 9 + 1;

    public static int[,] CanonicalSolvedGrid()
    {
        var grid = new int[9, 9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            grid[r, c] = CanonicalValueAt(r, c);
        return grid;
    }

    public static Board CanonicalSolvedBoard()
    {
        var board = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            board.Set(r, c, CanonicalValueAt(r, c));
        return board;
    }

    // The fixture puzzle: the canonical grid with PuzzleHoles left empty and
    // every remaining cell marked as a given. The recorded solution (when
    // requested) is the canonical grid itself.
    public static Board Puzzle(bool withSolution = true)
    {
        var board = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (IsHole(r, c)) continue;
            board.Set(r, c, CanonicalValueAt(r, c), given: true);
        }

        if (withSolution) board.SetSolution(CanonicalSolvedGrid());
        return board;
    }

    // Complete every empty cell with its recorded solution value through the
    // public Select/Place flow, exactly the way the UI drives the service.
    public static void FillFromSolution(IGameService game)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (game.Current.Get(r, c) is not null) continue;
            game.Select(r, c);
            game.Place(game.Current.SolutionAt(r, c)!.Value);
        }
    }

    // ----- flattening helpers --------------------------------------------
    // Tests assert on whole-board equality rather than looping over 81 cells
    // in the test body: xUnit compares arrays element-wise and reports the
    // first differing index, so the diagnostic is as good and the test stays
    // free of control flow.

    public static int?[] Values(Board board)
    {
        var values = new int?[81];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            values[r * 9 + c] = board.Get(r, c);
        return values;
    }

    public static bool[] Givens(Board board)
    {
        var givens = new bool[81];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            givens[r * 9 + c] = board[r, c].IsGiven;
        return givens;
    }

    public static int?[] Solution(Board board)
    {
        var solution = new int?[81];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            solution[r * 9 + c] = board.SolutionAt(r, c);
        return solution;
    }

    // Each cell's notes as a sorted array, so two boards' pencil marks compare
    // as one value regardless of hash-set ordering.
    public static int[][] Notes(Board board)
    {
        var notes = new int[81][];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            notes[r * 9 + c] = board[r, c].Notes.OrderBy(n => n).ToArray();
        return notes;
    }

    public static int FilledCellCount(Board board) => Values(board).Count(v => v is not null);

    // A board holding the puzzle's recorded solution in every cell.
    public static Board SolvedCopyOf(Board puzzle)
    {
        var solved = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            solved.Set(r, c, puzzle.SolutionAt(r, c));
        return solved;
    }

    private static bool IsHole(int row, int col)
    {
        foreach (var (r, c) in PuzzleHoles)
            if (r == row && c == col) return true;
        return false;
    }
}
