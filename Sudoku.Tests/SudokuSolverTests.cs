using Sudoku.Domain;

namespace Sudoku.Tests;

public class SudokuSolverTests
{
    [Fact]
    public void Solves_an_empty_board_to_a_complete_valid_grid()
    {
        var board = new Board();

        Assert.True(TestGame.Solver().TrySolve(board));
        Assert.True(TestGame.Validator().IsComplete(board));
    }

    [Fact]
    public void Returns_false_when_the_givens_conflict()
    {
        var board = new Board();
        board.Set(0, 0, 5);
        board.Set(0, 8, 5); // same row, same digit

        Assert.False(TestGame.Solver().TrySolve(board));
    }

    [Fact]
    public void A_failed_solve_leaves_the_board_untouched()
    {
        var board = new Board();
        board.Set(0, 0, 5);
        board.Set(0, 8, 5);

        TestGame.Solver().TrySolve(board);

        int filled = 0;
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (board.Get(r, c) is not null) filled++;
        Assert.Equal(2, filled);
    }

    [Fact]
    public void CountSolutions_reports_zero_for_a_contradictory_board()
    {
        var board = new Board();
        board.Set(0, 0, 5);
        board.Set(0, 8, 5);

        Assert.Equal(0, TestGame.Solver().CountSolutions(board, 2));
    }

    [Fact]
    public void CountSolutions_reports_one_for_a_solved_board()
    {
        var board = new Board();
        Assert.True(TestGame.Solver().TrySolve(board));

        Assert.Equal(1, TestGame.Solver().CountSolutions(board, 2));
    }

    [Fact]
    public void CountSolutions_stops_at_the_limit_on_a_wide_open_board()
    {
        // An empty grid has billions of completions; the counter must stop
        // at the limit instead of enumerating them.
        Assert.Equal(2, TestGame.Solver().CountSolutions(new Board(), 2));
    }

    [Fact]
    public void CountSolutions_does_not_mutate_the_board()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());
        var board = generator.Generate(Sudoku.Application.Models.Difficulty.Easy);

        var before = new int?[9, 9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            before[r, c] = board.Get(r, c);

        TestGame.Solver().CountSolutions(board, 2);

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            Assert.Equal(before[r, c], board.Get(r, c));
    }

    // The fast bitmask counter must agree with the naive reference counter that
    // the rest of the suite uses as its independent oracle.
    [Fact]
    public void Fast_counter_agrees_with_the_reference_counter()
    {
        var validator = TestGame.Validator();
        var generator = TestGame.Generator(validator, TestGame.Solver());
        var board = generator.Generate(Sudoku.Application.Models.Difficulty.Medium);

        Assert.Equal(
            TestGame.CountSolutions(board, validator),
            TestGame.Solver().CountSolutions(board, 2));
    }
}
