using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Tests;

public class SudokuServiceTests
{
    [Fact]
    public void Place_OnAGivenCell_LeavesTheClueUntouched()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = FirstGiven(svc.Current);
        var clue = svc.Current.Get(row, col);

        svc.Select(row, col);
        svc.Place(clue == 9 ? 1 : clue!.Value + 1);

        Assert.Equal(clue, svc.Current.Get(row, col));
    }

    [Fact]
    public void ClearAll_BoardWithPlayerEntries_RemovesThemAndKeepsEveryClue()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var givensBefore = TestBoards.Givens(svc.Current);
        var cluesBefore = CluesOnly(svc.Current);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);

        svc.ClearAll();

        Assert.Null(svc.Current.Get(row, col));
        Assert.Equal(cluesBefore, CluesOnly(svc.Current));
        Assert.Equal(givensBefore, TestBoards.Givens(svc.Current));
    }

    [Fact]
    public void Clear_OnAGivenCell_LeavesTheClueUntouched()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = FirstGiven(svc.Current);
        var clue = svc.Current.Get(row, col);

        svc.Select(row, col);
        svc.Clear();

        Assert.Equal(clue, svc.Current.Get(row, col));
    }

    // Solve used to run the backtracker over the live board, so it reported
    // "No solution" once the player had entered anything incorrect.
    [Fact]
    public void Solve_AfterAWrongPlayerEntry_StillCompletesTheBoard()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));

        Assert.True(svc.Solve());
        Assert.True(svc.IsComplete());
    }

    [Fact]
    public void Solve_GeneratedPuzzle_ReproducesTheRecordedSolutionExactly()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Medium);
        var expected = TestBoards.Solution(svc.Current);

        Assert.True(svc.Solve());

        Assert.Equal(expected, TestBoards.Values(svc.Current));
    }

    [Fact]
    public void Validate_FreshlyGeneratedBoard_IsValidButNotComplete()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Medium);

        Assert.True(svc.Validate());
        Assert.False(svc.IsComplete());
    }

    [Fact]
    public void HasConflict_DuplicateInTheSameRow_IsFlagged()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var clash = FirstClueInRow(svc.Current, row, col);

        svc.Select(row, col);
        svc.Place(clash);

        Assert.True(svc.HasConflict(row, col));
        Assert.False(svc.Validate());
    }

    [Fact]
    public void HasConflict_EmptyCell_IsNeverFlagged()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);

        Assert.False(svc.HasConflict(row, col));
    }

    // Clue values only, with player entries masked out.
    private static int?[] CluesOnly(Board board)
    {
        var values = TestBoards.Values(board);
        var givens = TestBoards.Givens(board);
        return values.Select((v, i) => givens[i] ? v : null).ToArray();
    }

    private static (int Row, int Col) FirstGiven(Board board)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (board[r, c].IsGiven) return (r, c);

        throw new InvalidOperationException("Board has no given cells.");
    }

    private static int FirstClueInRow(Board board, int row, int exceptCol)
    {
        for (int c = 0; c < 9; c++)
        {
            if (c == exceptCol) continue;
            if (board.Get(row, c) is int v) return v;
        }

        throw new InvalidOperationException($"Row {row} has no clues.");
    }
}
