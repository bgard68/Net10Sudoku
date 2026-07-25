using Sudoku.Application.Implementations;
using Sudoku.Application.Services;
using Sudoku.Domain;
using Sudoku.Infrastructure;

namespace Sudoku.Tests;

// Builds the real service graph by hand. The production DI container wires the same
// implementations, so these tests exercise the shipped behaviour rather than fakes.
internal static class TestGame
{
    public static SudokuValidator Validator() => new();

    public static SudokuSolver Solver(SudokuValidator validator) => new(validator);

    public static SudokuGenerator Generator(SudokuValidator validator, SudokuSolver solver) => new(solver, validator, new PuzzleGrader());

    public static SudokuService Service()
    {
        var validator = Validator();
        var solver = Solver(validator);
        return new SudokuService(
            Generator(validator, solver),
            solver,
            validator,
            new SudokuHintProvider(validator),
            new ConflictDetector(validator),
            new GameState());
    }

    // First cell the player is allowed to fill.
    public static (int Row, int Col) FirstEmptyCell(Board board)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (board.Get(r, c) is null) return (r, c);

        throw new InvalidOperationException("Board has no empty cells.");
    }

    // A value that is definitely not the answer for this cell.
    public static int WrongValueFor(Board board, int row, int col)
    {
        var correct = board.SolutionAt(row, col)
            ?? throw new InvalidOperationException("Board has no recorded solution.");
        return correct == 9 ? 1 : correct + 1;
    }

    public static int CountSolutions(Board board, SudokuValidator validator, int limit = 2)
    {
        var work = board.Clone();
        int count = 0;
        Recurse(work, validator, ref count, limit);
        return count;
    }

    private static bool Recurse(Board board, SudokuValidator validator, ref int count, int limit)
    {
        if (count >= limit) return true;

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (board.Get(r, c) is not null) continue;

            for (int v = 1; v <= 9; v++)
            {
                if (!validator.CanPlace(board, r, c, v)) continue;
                board.Cells[r, c].Set(v);
                if (Recurse(board, validator, ref count, limit))
                {
                    board.Cells[r, c].Set(null);
                    return true;
                }
                board.Cells[r, c].Set(null);
            }
            return false;
        }

        if (validator.IsValid(board)) count++;
        return false;
    }
}
