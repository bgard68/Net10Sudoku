using Sudoku.Domain;

namespace Sudoku.Tests;

public class BoardTests
{
    [Fact]
    public void Clone_copies_values_and_given_flags()
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
    public void Clone_is_independent_of_the_original()
    {
        var board = new Board();
        board.Set(2, 2, 4);

        var copy = board.Clone();
        copy.Set(2, 2, 9);

        Assert.Equal(4, board.Get(2, 2));
        Assert.Equal(9, copy.Get(2, 2));
    }

    [Fact]
    public void Clone_carries_the_recorded_solution()
    {
        var board = new Board();
        board.SetSolution(FilledGrid());

        var copy = board.Clone();

        Assert.True(copy.HasSolution);
        Assert.Equal(board.SolutionAt(4, 4), copy.SolutionAt(4, 4));
    }

    [Fact]
    public void A_board_has_no_solution_until_one_is_recorded()
    {
        var board = new Board();

        Assert.False(board.HasSolution);
        Assert.Null(board.SolutionAt(0, 0));
    }

    [Fact]
    public void SetSolution_defends_against_a_wrongly_sized_grid()
    {
        var board = new Board();

        Assert.Throws<ArgumentException>(() => board.SetSolution(new int[3, 3]));
    }

    [Fact]
    public void SetSolution_takes_a_copy_so_later_edits_do_not_leak_in()
    {
        var board = new Board();
        var grid = FilledGrid();
        board.SetSolution(grid);

        grid[0, 0] = 9;

        Assert.NotEqual(9, board.SolutionAt(0, 0));
    }

    [Fact]
    public void A_given_cell_cannot_be_overwritten_by_the_player()
    {
        var board = new Board();
        board.Set(0, 0, 6, given: true);

        Assert.Throws<InvalidOperationException>(() => board.Set(0, 0, 1));
    }

    private static int[,] FilledGrid()
    {
        var grid = new int[9, 9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            grid[r, c] = (r * 3 + r / 3 + c) % 9 + 1;
        return grid;
    }
}
