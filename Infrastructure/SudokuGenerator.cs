using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

public sealed class SudokuGenerator : ISudokuGenerator
{
    private readonly ISudokuSolver _solver;
    private readonly ISudokuValidator _validator;
    // Random is not thread-safe; Random.Shared is thread-safe for concurrent Next() calls
    private static Random Rng => Random.Shared;

    public SudokuGenerator(ISudokuSolver solver, ISudokuValidator validator)
    {
        _solver = solver;
        _validator = validator;
    }

    public Board Generate(Difficulty difficulty)
    {
        var board = new Board();

        // Generate a complete valid board by solving an empty board using random ordering.
        FillDiagonalBoxes(board);
        _solver.TrySolve(board);

        // Remove numbers based on difficulty while keeping a unique solution (simple heuristic).
        int removals = difficulty switch
        {
            Difficulty.Easy => 40,
            Difficulty.Medium => 50,
            Difficulty.Hard => 55,
            Difficulty.Professional => 60,
            _ => 50
        };

        var positions = Enumerable.Range(0,81).OrderBy(_ => Rng.Next()).ToList();
        foreach (var idx in positions)
        {
            if (removals <= 0) break;
            int r = idx / 9, c = idx % 9;
            var prev = board.Get(r,c);
            if (prev is null) continue;
            board.Set(r,c,null);

            if (!HasUniqueSolution(board.Clone()))
            {
                board.Set(r,c,prev);
            }
            else
            {
                board.Cells[r,c].Set(null, given: false);
                removals--;
            }
        }

        // Mark remaining numbers as given
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (board.Get(r,c) is not null)
                board.Cells[r,c].Set(board.Get(r,c), given: true);
        }

        return board;
    }

    private void FillDiagonalBoxes(Board board)
    {
        for (int b = 0; b < 3; b++)
        {
            var nums = Enumerable.Range(1,9).OrderBy(_ => Rng.Next()).ToArray();
            int k = 0;
            for (int r = b*3; r < b*3+3; r++)
            for (int c = b*3; c < b*3+3; c++)
                board.Set(r,c, nums[k++]);
        }
    }

    private bool HasUniqueSolution(Board board)
    {
        int count = 0;
        CountSolutions(board, ref count, 2);
        return count == 1;
    }

    private bool CountSolutions(Board board, ref int count, int limit)
    {
        if (count >= limit) return true;
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (board.Get(r,c) is not null) continue;
            for (int v = 1; v <= 9; v++)
            {
                if (!_validator.CanPlace(board, r, c, v)) continue;
                board.Set(r,c,v);
                if (CountSolutions(board, ref count, limit)) { board.Set(r,c,null); return true; }
                board.Set(r,c,null);
            }
            return false;
        }
        if (_validator.IsValid(board)) count++;
        return false;
    }
}
