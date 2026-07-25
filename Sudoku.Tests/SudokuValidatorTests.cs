using Sudoku.Domain;

namespace Sudoku.Tests;

public class SudokuValidatorTests
{
    [Fact]
    public void CanPlace_rejects_a_value_already_in_the_row()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.False(TestGame.Validator().CanPlace(board, 0, 8, 5));
    }

    [Fact]
    public void CanPlace_rejects_a_value_already_in_the_column()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.False(TestGame.Validator().CanPlace(board, 8, 0, 5));
    }

    [Fact]
    public void CanPlace_rejects_a_value_already_in_the_box()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.False(TestGame.Validator().CanPlace(board, 2, 2, 5));
    }

    [Fact]
    public void CanPlace_allows_a_value_that_breaks_no_rule()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.True(TestGame.Validator().CanPlace(board, 8, 8, 5));
    }

    [Fact]
    public void CanPlace_ignores_the_target_cell_itself()
    {
        var board = new Board();
        board.Set(4, 4, 7);

        // Re-placing the same value in the cell it already occupies is not a conflict.
        Assert.True(TestGame.Validator().CanPlace(board, 4, 4, 7));
    }

    [Fact]
    public void An_empty_board_is_valid_but_not_complete()
    {
        var validator = TestGame.Validator();
        var board = new Board();

        Assert.True(validator.IsValid(board));
        Assert.False(validator.IsComplete(board));
    }

    [Fact]
    public void A_board_with_a_duplicate_in_a_row_is_not_valid()
    {
        var board = new Board();
        board.Set(3, 1, 4);
        board.Set(3, 7, 4);

        Assert.False(TestGame.Validator().IsValid(board));
    }
}
