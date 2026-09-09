using Sudoku.Domain;

namespace Sudoku.Tests;

public class BoardTests
{
    [Fact]
    public void Clone_BoardWithValuesAndGivens_CopiesBothFaithfully()
    {
        var board = new Board();
        board.Set(0, 0, 3, given: true);
        board.Set(1, 1, 7);

        var copy = board.Clone();

        Assert.Equal(3, copy.Get(0, 0));
        Assert.True(copy[0, 0].IsGiven);
        Assert.Equal(7, copy.Get(1, 1));
        Assert.False(copy[1, 1].IsGiven);
    }

    [Fact]
    public void Clone_EditingTheCopy_LeavesTheOriginalUnchanged()
    {
        var board = new Board();
        board.Set(2, 2, 4);

        var copy = board.Clone();
        copy.Set(2, 2, 9);

        Assert.Equal(4, board.Get(2, 2));
        Assert.Equal(9, copy.Get(2, 2));
    }

    [Fact]
    public void Clone_BoardWithRecordedSolution_CarriesTheWholeSolution()
    {
        var board = new Board();
        board.SetSolution(TestBoards.CanonicalSolvedGrid());

        var copy = board.Clone();

        Assert.True(copy.HasSolution);
        Assert.Equal(TestBoards.Solution(board), TestBoards.Solution(copy));
    }

    [Fact]
    public void HasSolution_BeforeAnySolutionIsRecorded_IsFalse()
    {
        var board = new Board();

        Assert.False(board.HasSolution);
        Assert.Null(board.SolutionAt(0, 0));
    }

    [Fact]
    public void SetSolution_WronglySizedGrid_ThrowsArgumentException()
    {
        var board = new Board();

        Assert.Throws<ArgumentException>(() => board.SetSolution(new int[3, 3]));
    }

    [Fact]
    public void SetSolution_CallerMutatesTheGridAfterwards_DoesNotAffectTheBoard()
    {
        var board = new Board();
        var grid = TestBoards.CanonicalSolvedGrid();
        board.SetSolution(grid);

        grid[0, 0] = 9;

        Assert.Equal(TestBoards.CanonicalValueAt(0, 0), board.SolutionAt(0, 0));
        Assert.NotEqual(9, board.SolutionAt(0, 0));
    }

    [Fact]
    public void Set_PlayerWriteOverAGivenCell_ThrowsInvalidOperationException()
    {
        var board = new Board();
        board.Set(0, 0, 6, given: true);

        Assert.Throws<InvalidOperationException>(() => board.Set(0, 0, 1));
    }
}
