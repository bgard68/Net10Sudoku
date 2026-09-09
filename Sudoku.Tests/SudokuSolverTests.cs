using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Tests;

public class SudokuSolverTests
{
    [Fact]
    public void TrySolve_EmptyBoard_ProducesACompleteValidGrid()
    {
        var board = new Board();

        Assert.True(TestGame.Solver().TrySolve(board));
        Assert.True(TestGame.Validator().IsComplete(board));
    }

    [Fact]
    public void TrySolve_ConflictingGivens_ReturnsFalse()
    {
        var board = new Board();
        board.Set(0, 0, 5);
        board.Set(0, 8, 5); // same row, same digit

        Assert.False(TestGame.Solver().TrySolve(board));
    }

    [Fact]
    public void TrySolve_ConflictingGivens_LeavesTheBoardExactlyAsItWas()
    {
        var board = new Board();
        board.Set(0, 0, 5);
        board.Set(0, 8, 5);
        var before = TestBoards.Values(board);

        TestGame.Solver().TrySolve(board);

        Assert.Equal(before, TestBoards.Values(board));
        Assert.Equal(2, TestBoards.FilledCellCount(board));
    }

    [Fact]
    public void TrySolve_NullBoard_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => TestGame.Solver().TrySolve(null!));

        Assert.Equal("board", ex.ParamName);
    }

    [Fact]
    public void CountSolutions_ContradictoryBoard_ReturnsZero()
    {
        var board = new Board();
        board.Set(0, 0, 5);
        board.Set(0, 8, 5);

        Assert.Equal(0, TestGame.Solver().CountSolutions(board, 2));
    }

    [Fact]
    public void CountSolutions_SolvedBoard_ReturnsOne()
    {
        Assert.Equal(1, TestGame.Solver().CountSolutions(TestBoards.CanonicalSolvedBoard(), 2));
    }

    // An empty grid has billions of completions; the counter must stop at the
    // limit instead of enumerating them.
    [Fact]
    public void CountSolutions_WideOpenBoard_StopsAtTheLimit()
    {
        Assert.Equal(2, TestGame.Solver().CountSolutions(new Board(), 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CountSolutions_NonPositiveLimit_ReturnsZeroWithoutSearching(int limit)
    {
        Assert.Equal(0, TestGame.Solver().CountSolutions(new Board(), limit));
    }

    [Fact]
    public void CountSolutions_AnyBoard_LeavesItUnmodified()
    {
        var board = TestGame.Generator(TestGame.Validator(), TestGame.Solver()).Generate(Difficulty.Easy);
        var before = TestBoards.Values(board);

        TestGame.Solver().CountSolutions(board, 2);

        Assert.Equal(before, TestBoards.Values(board));
    }

    // The fast bitmask counter must agree with the naive reference counter that
    // the rest of the suite uses as its independent oracle.
    [Fact]
    public void CountSolutions_GeneratedPuzzle_AgreesWithTheReferenceCounter()
    {
        var validator = TestGame.Validator();
        var board = TestGame.Generator(validator, TestGame.Solver()).Generate(Difficulty.Medium);

        Assert.Equal(
            TestGame.CountSolutions(board, validator),
            TestGame.Solver().CountSolutions(board, 2));
    }
}
