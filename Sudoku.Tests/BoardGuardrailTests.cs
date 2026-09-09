using Sudoku.Domain;

namespace Sudoku.Tests;

// The board is the domain's validation boundary: off-grid coordinates and
// impossible digits must be rejected with argument exceptions instead of
// leaking through as raw index faults or silently corrupting solver bitmasks.
public class BoardGuardrailTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void Indexer_RowOutOfRange_ThrowsArgumentOutOfRangeException(int row)
    {
        var board = new Board();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => board[row, 0]);

        Assert.Equal("row", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void Indexer_ColOutOfRange_ThrowsArgumentOutOfRangeException(int col)
    {
        var board = new Board();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => board[0, col]);

        Assert.Equal("col", ex.ParamName);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 9)]
    public void Get_OffGridCoordinates_ThrowsArgumentOutOfRangeException(int row, int col)
    {
        var board = new Board();

        Assert.Throws<ArgumentOutOfRangeException>(() => board.Get(row, col));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 9)]
    public void Set_OffGridCoordinates_ThrowsArgumentOutOfRangeException(int row, int col)
    {
        var board = new Board();

        Assert.Throws<ArgumentOutOfRangeException>(() => board.Set(row, col, 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(-3)]
    public void Set_DigitOutsideOneToNine_ThrowsArgumentOutOfRangeException(int value)
    {
        var board = new Board();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => board.Set(0, 0, value));

        Assert.Equal("v", ex.ParamName);
        Assert.Null(board.Get(0, 0));
    }

    [Fact]
    public void Set_BoundaryDigitsOneAndNine_AreAccepted()
    {
        var board = new Board();

        board.Set(0, 0, 1);
        board.Set(8, 8, 9);

        Assert.Equal(1, board.Get(0, 0));
        Assert.Equal(9, board.Get(8, 8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ToggleNote_DigitOutsideOneToNine_ThrowsArgumentOutOfRangeException(int value)
    {
        var board = new Board();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => board.ToggleNote(0, 0, value));

        Assert.Equal("value", ex.ParamName);
        Assert.Empty(board[0, 0].Notes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void RemoveNote_DigitOutsideOneToNine_ThrowsArgumentOutOfRangeException(int value)
    {
        var board = new Board();

        Assert.Throws<ArgumentOutOfRangeException>(() => board.RemoveNote(0, 0, value));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 9)]
    public void SolutionAt_OffGridCoordinates_ThrowsArgumentOutOfRangeException(int row, int col)
    {
        var board = new Board();
        board.SetSolution(TestBoards.CanonicalSolvedGrid());

        Assert.Throws<ArgumentOutOfRangeException>(() => board.SolutionAt(row, col));
    }

    [Fact]
    public void SetSolution_NullGrid_ThrowsArgumentNullException()
    {
        var board = new Board();

        var ex = Assert.Throws<ArgumentNullException>(() => board.SetSolution(null!));

        Assert.Equal("solution", ex.ParamName);
        Assert.False(board.HasSolution);
    }

    [Fact]
    public void SetSolution_GridContainingZero_ThrowsArgumentException()
    {
        var board = new Board();
        var grid = TestBoards.CanonicalSolvedGrid();
        grid[4, 4] = 0;

        var ex = Assert.Throws<ArgumentException>(() => board.SetSolution(grid));

        Assert.Equal("solution", ex.ParamName);
        Assert.False(board.HasSolution);
    }

    [Fact]
    public void SetSolution_GridContainingTen_ThrowsArgumentException()
    {
        var board = new Board();
        var grid = TestBoards.CanonicalSolvedGrid();
        grid[0, 8] = 10;

        Assert.Throws<ArgumentException>(() => board.SetSolution(grid));

        Assert.False(board.HasSolution);
    }

    [Fact]
    public void Clone_CellWithNotes_CopiesTheNotes()
    {
        var board = new Board();
        board.ToggleNote(2, 3, 4);
        board.ToggleNote(2, 3, 8);

        var copy = board.Clone();

        Assert.True(copy[2, 3].HasNote(4));
        Assert.True(copy[2, 3].HasNote(8));
        Assert.Equal(2, copy[2, 3].Notes.Count);
    }

    [Fact]
    public void Clone_NoteChangesOnTheCopy_DoNotReachTheOriginal()
    {
        var board = new Board();
        board.ToggleNote(2, 3, 4);
        var copy = board.Clone();

        copy.ToggleNote(2, 3, 4);

        Assert.True(board[2, 3].HasNote(4));
        Assert.False(copy[2, 3].HasNote(4));
    }
}
