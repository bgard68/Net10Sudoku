using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class SudokuGeneratorTests
{
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Professional)]
    public void Generated_puzzles_have_exactly_one_solution(Difficulty difficulty)
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver(validator));

        var board = generator.Generate(difficulty);

        Assert.Equal(1, TestGame.CountSolutions(board, validator));
    }

    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    [InlineData(Difficulty.Professional)]
    public void Generated_puzzles_record_a_solution_that_agrees_with_the_givens(Difficulty difficulty)
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver(validator));

        var board = generator.Generate(difficulty);

        Assert.True(board.HasSolution);
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var solved = board.SolutionAt(r, c);
            Assert.NotNull(solved);
            Assert.InRange(solved!.Value, 1, 9);

            if (board.Get(r, c) is int clue)
                Assert.Equal(clue, solved.Value);
        }
    }

    [Fact]
    public void Recorded_solution_is_itself_a_complete_valid_grid()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver(validator));

        var board = generator.Generate(Difficulty.Medium);

        var filled = new Domain.Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            filled.Cells[r, c].Set(board.SolutionAt(r, c));

        Assert.True(validator.IsComplete(filled));
    }

    [Fact]
    public void Every_cell_the_player_can_edit_is_empty_and_every_clue_is_marked_given()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver(validator));

        var board = generator.Generate(Difficulty.Easy);

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var cell = board.Cells[r, c];
            Assert.Equal(cell.Value is not null, cell.IsGiven);
        }
    }

    // Medium and Hard clue counts overlap run to run, so only the stable
    // relationship is asserted: Easy always keeps noticeably more clues.
    [Fact]
    public void Easy_puzzles_leave_more_clues_than_harder_ones()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver(validator));

        int easy = Givens(generator.Generate(Difficulty.Easy));
        int medium = Givens(generator.Generate(Difficulty.Medium));
        int hard = Givens(generator.Generate(Difficulty.Hard));

        Assert.True(easy > medium, $"Easy ({easy}) should leave more clues than Medium ({medium}).");
        Assert.True(easy > hard, $"Easy ({easy}) should leave more clues than Hard ({hard}).");
    }

    private static int Givens(Domain.Board board)
    {
        int n = 0;
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (board.Get(r, c) is not null) n++;
        return n;
    }
}
