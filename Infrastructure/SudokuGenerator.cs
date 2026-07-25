using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

public sealed class SudokuGenerator : ISudokuGenerator
{
    // Carving is cheap next to the value of an on-band puzzle, so retry generously.
    // Hard boards land in band roughly every other attempt; the cap is a backstop.
    private const int MaxAttempts = 60;

    private readonly ISudokuSolver _solver;
    private readonly ISudokuValidator _validator;
    private readonly IPuzzleGrader _grader;
    // Random is not thread-safe; Random.Shared is thread-safe for concurrent Next() calls
    private static Random Rng => Random.Shared;

    public SudokuGenerator(ISudokuSolver solver, ISudokuValidator validator, IPuzzleGrader grader)
    {
        _solver = solver;
        _validator = validator;
        _grader = grader;
    }

    // Clue count alone is a poor proxy for difficulty - about half of the boards dug
    // to "Hard" depth fall to singles. So each candidate is graded by the techniques
    // it actually requires, and carving repeats until the grade lands in the band.
    public Board Generate(Difficulty difficulty)
    {
        Board? closest = null;
        int closestDistance = int.MaxValue;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var board = Carve(Removals(difficulty));
            var tier = _grader.Grade(board);
            if (InBand(difficulty, tier)) return board;

            var distance = DistanceToBand(difficulty, tier);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = board;
            }
        }

        // Every carved candidate is verified unique, so the nearest miss is still a
        // sound puzzle - just outside the ideal difficulty band.
        return closest!;
    }

    private static int Removals(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 40,
        Difficulty.Medium => 50,
        Difficulty.Hard => 55,
        Difficulty.Professional => 60,
        _ => 50
    };

    // Hard and Professional both demand more than the cheap techniques; they differ
    // by how many clues remain to work with.
    private static bool InBand(Difficulty difficulty, TechniqueTier tier) => difficulty switch
    {
        Difficulty.Easy => tier == TechniqueTier.Singles,
        Difficulty.Medium => tier is TechniqueTier.LockedCandidate or TechniqueTier.Pair,
        _ => tier == TechniqueTier.Advanced
    };

    // A miss below the band beats a miss above it: a Medium that plays slightly
    // easy is a mild disappointment, a Medium that needs chains is a wall. So
    // Advanced is scored far from the Medium band while Singles sits adjacent.
    private static int DistanceToBand(Difficulty difficulty, TechniqueTier tier) => difficulty switch
    {
        Difficulty.Easy => (int)tier,
        Difficulty.Medium => tier switch
        {
            TechniqueTier.LockedCandidate or TechniqueTier.Pair => 0,
            TechniqueTier.Singles => 1,
            _ => 3
        },
        _ => (int)TechniqueTier.Advanced - (int)tier
    };

    private Board Carve(int removals)
    {
        var board = new Board();

        // Generate a complete valid board by solving an empty board using random ordering.
        FillDiagonalBoxes(board);
        _solver.TrySolve(board);

        // Capture the completed grid before digging. Because every removal below is
        // reverted unless the board still has exactly one solution, this stays the
        // puzzle's unique answer and lets hints avoid re-solving a board the player
        // may have entered wrong values into.
        var solution = new int[9,9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            solution[r,c] = board.Get(r,c) ?? 0;
        board.SetSolution(solution);

        // Remove numbers while keeping a unique solution.
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
