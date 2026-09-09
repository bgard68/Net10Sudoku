using Sudoku.Domain;

namespace Sudoku.Tests;

public class SudokuValidatorTests
{
    [Fact]
    public void CanPlace_ValueAlreadyInTheRow_ReturnsFalse()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.False(TestGame.Validator().CanPlace(board, 0, 8, 5));
    }

    [Fact]
    public void CanPlace_ValueAlreadyInTheColumn_ReturnsFalse()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.False(TestGame.Validator().CanPlace(board, 8, 0, 5));
    }

    [Fact]
    public void CanPlace_ValueAlreadyInTheBox_ReturnsFalse()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.False(TestGame.Validator().CanPlace(board, 2, 2, 5));
    }

    [Fact]
    public void CanPlace_ValueBreakingNoRule_ReturnsTrue()
    {
        var board = new Board();
        board.Set(0, 0, 5);

        Assert.True(TestGame.Validator().CanPlace(board, 8, 8, 5));
    }

    [Fact]
    public void CanPlace_SameValueInTheCellItAlreadyOccupies_ReturnsTrue()
    {
        var board = new Board();
        board.Set(4, 4, 7);

        Assert.True(TestGame.Validator().CanPlace(board, 4, 4, 7));
    }

    [Theory]
    [InlineData(-1, 0, 5)]
    [InlineData(9, 0, 5)]
    [InlineData(0, -1, 5)]
    [InlineData(0, 9, 5)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, 10)]
    public void CanPlace_ArgumentsOutsideTheGrid_ThrowArgumentOutOfRangeException(int row, int col, int value)
    {
        var board = new Board();

        Assert.Throws<ArgumentOutOfRangeException>(() => TestGame.Validator().CanPlace(board, row, col, value));
    }

    [Fact]
    public void IsValid_NullBoard_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => TestGame.Validator().IsValid(null!));

        Assert.Equal("board", ex.ParamName);
    }

    [Fact]
    public void IsComplete_EmptyBoard_IsValidButNotComplete()
    {
        var validator = TestGame.Validator();
        var board = new Board();

        Assert.True(validator.IsValid(board));
        Assert.False(validator.IsComplete(board));
    }

    [Fact]
    public void IsValid_DuplicateInARow_ReturnsFalse()
    {
        var board = new Board();
        board.Set(3, 1, 4);
        board.Set(3, 7, 4);

        Assert.False(TestGame.Validator().IsValid(board));
    }

    [Fact]
    public void IsValid_DuplicateInAColumn_ReturnsFalse()
    {
        var board = new Board();
        board.Set(1, 5, 8);
        board.Set(7, 5, 8);

        Assert.False(TestGame.Validator().IsValid(board));
    }

    [Fact]
    public void IsValid_DuplicateInABox_ReturnsFalse()
    {
        var board = new Board();
        board.Set(3, 3, 2);
        board.Set(5, 5, 2);

        Assert.False(TestGame.Validator().IsValid(board));
    }

    [Fact]
    public void IsComplete_FullyAndCorrectlyFilledBoard_ReturnsTrue()
    {
        Assert.True(TestGame.Validator().IsComplete(TestBoards.CanonicalSolvedBoard()));
    }
}
