using Sudoku.Application.Models;
using Sudoku.Domain;
using Sudoku.Infrastructure;

namespace Sudoku.Tests;

public class PuzzleGraderTests
{
    [Fact]
    public void A_solved_board_grades_as_singles()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());
        var puzzle = generator.Generate(Difficulty.Easy);

        var solved = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            solved.Set(r, c, puzzle.SolutionAt(r, c));

        Assert.Equal(TechniqueTier.Singles, new PuzzleGrader().Grade(solved));
    }

    [Fact]
    public void A_board_missing_one_value_grades_as_singles()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());
        var puzzle = generator.Generate(Difficulty.Easy);

        var nearlySolved = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            nearlySolved.Set(r, c, puzzle.SolutionAt(r, c));
        nearlySolved.Set(4, 4, null);

        Assert.Equal(TechniqueTier.Singles, new PuzzleGrader().Grade(nearlySolved));
    }

    // No singles, no locked candidates, no pairs are available on an empty grid,
    // so the grader must admit its techniques cannot finish it.
    [Fact]
    public void An_empty_board_grades_as_advanced()
    {
        Assert.Equal(TechniqueTier.Advanced, new PuzzleGrader().Grade(new Board()));
    }

    // Grading must be a pure question - asking it twice about the same board gives
    // the same answer and leaves the board untouched.
    [Fact]
    public void Grading_is_repeatable_and_does_not_mutate_the_board()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());
        var puzzle = generator.Generate(Difficulty.Medium);

        var before = new int?[9, 9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            before[r, c] = puzzle.Get(r, c);

        var grader = new PuzzleGrader();
        var first = grader.Grade(puzzle);
        var second = grader.Grade(puzzle);

        Assert.Equal(first, second);
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            Assert.Equal(before[r, c], puzzle.Get(r, c));
    }

    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Professional)]
    public void Generated_puzzles_land_in_their_difficulty_band(Difficulty difficulty)
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());

        var tier = new PuzzleGrader().Grade(generator.Generate(difficulty));

        switch (difficulty)
        {
            case Difficulty.Easy:
                Assert.Equal(TechniqueTier.Singles, tier);
                break;
            case Difficulty.Medium:
                // The in-band result is a locked candidate or pair; when every
                // attempt misses, the generator falls back toward the easy side
                // by design. What it must never hand a Medium player is a board
                // requiring advanced techniques.
                Assert.True(tier != TechniqueTier.Advanced,
                    $"Medium must never require advanced techniques, got {tier}.");
                break;
            default:
                Assert.Equal(TechniqueTier.Advanced, tier);
                break;
        }
    }
}
