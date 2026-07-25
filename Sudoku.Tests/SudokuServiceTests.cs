using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class SudokuServiceTests
{
    [Fact]
    public void Place_leaves_given_cells_untouched()
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
    public void ClearAll_removes_player_entries_but_keeps_the_clues()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);

        svc.ClearAll();

        Assert.Null(svc.Current.Get(row, col));
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (svc.Current[r, c].IsGiven)
                Assert.NotNull(svc.Current.Get(r, c));
        }
    }

    [Fact]
    public void Clear_leaves_given_cells_untouched()
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
    public void Solve_completes_the_board_despite_a_wrong_player_entry()
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
    public void Solve_produces_the_recorded_solution()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Medium);
        var expected = Snapshot(svc.Current);

        Assert.True(svc.Solve());

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            Assert.Equal(expected[r, c], svc.Current.Get(r, c));
    }

    [Fact]
    public void A_freshly_generated_board_is_valid_but_not_complete()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Medium);

        Assert.True(svc.Validate());
        Assert.False(svc.IsComplete());
    }

    [Fact]
    public void HasConflict_flags_a_duplicate_in_the_same_row()
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

    private static int[,] Snapshot(Domain.Board board)
    {
        var grid = new int[9, 9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            grid[r, c] = board.SolutionAt(r, c)!.Value;
        return grid;
    }

    private static (int Row, int Col) FirstGiven(Domain.Board board)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (board[r, c].IsGiven) return (r, c);

        throw new InvalidOperationException("Board has no given cells.");
    }

    private static int FirstClueInRow(Domain.Board board, int row, int exceptCol)
    {
        for (int c = 0; c < 9; c++)
        {
            if (c == exceptCol) continue;
            if (board.Get(row, c) is int v) return v;
        }

        throw new InvalidOperationException($"Row {row} has no clues.");
    }
}
