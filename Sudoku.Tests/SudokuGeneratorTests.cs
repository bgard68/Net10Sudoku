using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Tests;

public class SudokuGeneratorTests
{
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Professional)]
    public void Generate_AnyDifficulty_ProducesAPuzzleWithExactlyOneSolution(Difficulty difficulty)
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());

        var board = generator.Generate(difficulty);

        Assert.Equal(1, TestGame.CountSolutions(board, validator));
    }

    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Professional)]
    public void Generate_AnyDifficulty_RecordsASolutionThatAgreesWithEveryClue(Difficulty difficulty)
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());

        var board = generator.Generate(difficulty);

        Assert.True(board.HasSolution);
        Assert.Empty(SolutionDigitsOutsideOneToNine(board));
        Assert.Empty(CluesContradictingTheSolution(board));
    }

    [Fact]
    public void Generate_RecordedSolution_IsItselfACompleteValidGrid()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());

        var board = generator.Generate(Difficulty.Medium);

        Assert.True(validator.IsComplete(TestBoards.SolvedCopyOf(board)));
    }

    [Fact]
    public void Generate_EveryCell_IsEitherAMarkedClueOrEmptyAndEditable()
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());

        var board = generator.Generate(Difficulty.Easy);

        // A cell holds a value exactly when it is a given, for all 81 cells.
        Assert.Equal(TestBoards.Values(board).Select(v => v is not null), TestBoards.Givens(board));
    }

    // Medium and Hard clue counts overlap run to run, so only the stable
    // relationship is asserted: Easy always keeps noticeably more clues.
    [Fact]
    public void Generate_EasyPuzzle_LeavesMoreCluesThanHarderOnes()
    {
        var generator = TestGame.Generator(TestGame.Validator(), TestGame.Solver());

        int easy = TestBoards.FilledCellCount(generator.Generate(Difficulty.Easy));
        int medium = TestBoards.FilledCellCount(generator.Generate(Difficulty.Medium));
        int hard = TestBoards.FilledCellCount(generator.Generate(Difficulty.Hard));

        Assert.True(easy > medium, $"Easy ({easy}) should leave more clues than Medium ({medium}).");
        Assert.True(easy > hard, $"Easy ({easy}) should leave more clues than Hard ({hard}).");
    }

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullExceptionNamingTheParameter()
    {
        var noSolver = Assert.Throws<ArgumentNullException>(
            () => new Infrastructure.SudokuGenerator(null!, new Infrastructure.PuzzleGrader()));
        var noGrader = Assert.Throws<ArgumentNullException>(
            () => new Infrastructure.SudokuGenerator(TestGame.Solver(), null!));

        Assert.Equal("solver", noSolver.ParamName);
        Assert.Equal("grader", noGrader.ParamName);
    }

    private static List<string> SolutionDigitsOutsideOneToNine(Board board) =>
        TestBoards.Solution(board)
            .Select((value, i) => (value, i))
            .Where(x => x.value is not (>= 1 and <= 9))
            .Select(x => $"cell {x.i} = {x.value?.ToString() ?? "null"}")
            .ToList();

    private static List<string> CluesContradictingTheSolution(Board board)
    {
        var values = TestBoards.Values(board);
        var solution = TestBoards.Solution(board);

        return values
            .Select((clue, i) => (clue, i))
            .Where(x => x.clue is not null && x.clue != solution[x.i])
            .Select(x => $"cell {x.i}: clue {x.clue} vs solution {solution[x.i]}")
            .ToList();
    }
}
