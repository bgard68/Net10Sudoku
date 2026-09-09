using Sudoku.Application.Models;
using Sudoku.Domain;
using Sudoku.Infrastructure;

namespace Sudoku.Tests;

public class PuzzleGraderTests
{
    [Fact]
    public void Grade_SolvedBoard_ReturnsSingles()
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());
        var solved = TestBoards.SolvedCopyOf(generator.Generate(Difficulty.Easy));

        Assert.Equal(TechniqueTier.Singles, new PuzzleGrader().Grade(solved));
    }

    [Fact]
    public void Grade_BoardMissingASingleValue_ReturnsSingles()
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());
        var nearlySolved = TestBoards.SolvedCopyOf(generator.Generate(Difficulty.Easy));
        nearlySolved.Set(4, 4, null);

        Assert.Equal(TechniqueTier.Singles, new PuzzleGrader().Grade(nearlySolved));
    }

    // No singles, no locked candidates, no pairs are available on an empty grid,
    // so the grader must admit its techniques cannot finish it.
    [Fact]
    public void Grade_EmptyBoard_ReturnsAdvanced()
    {
        Assert.Equal(TechniqueTier.Advanced, new PuzzleGrader().Grade(new Board()));
    }

    // Grading must be a pure question - asking it twice about the same board
    // gives the same answer and leaves the board untouched.
    [Fact]
    public void Grade_CalledTwiceOnOneBoard_IsRepeatableAndNonMutating()
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());
        var puzzle = generator.Generate(Difficulty.Medium);
        var before = TestBoards.Values(puzzle);
        var grader = new PuzzleGrader();

        var first = grader.Grade(puzzle);
        var second = grader.Grade(puzzle);

        Assert.Equal(first, second);
        Assert.Equal(before, TestBoards.Values(puzzle));
    }

    [Fact]
    public void Grade_GeneratedEasyPuzzle_NeedsOnlySingles()
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());

        var tier = new PuzzleGrader().Grade(generator.Generate(Difficulty.Easy));

        Assert.Equal(TechniqueTier.Singles, tier);
    }

    // The in-band result is a locked candidate or pair; when every attempt
    // misses, the generator falls back toward the easy side by design. What it
    // must never hand a Medium player is a board requiring advanced techniques.
    [Fact]
    public void Grade_GeneratedMediumPuzzle_NeverRequiresAdvancedTechniques()
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());

        var tier = new PuzzleGrader().Grade(generator.Generate(Difficulty.Medium));

        Assert.NotEqual(TechniqueTier.Advanced, tier);
    }

    [Theory]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Professional)]
    public void Grade_GeneratedHardPuzzle_RequiresAdvancedTechniques(Difficulty difficulty)
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());

        var tier = new PuzzleGrader().Grade(generator.Generate(difficulty));

        Assert.Equal(TechniqueTier.Advanced, tier);
    }
}
