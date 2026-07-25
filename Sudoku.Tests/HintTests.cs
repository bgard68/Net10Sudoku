using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class HintTests
{
    // Regression: hints used to be derived by solving the live board, so a single
    // incorrect entry anywhere made the puzzle unsolvable and every hint returned null.
    [Fact]
    public void Hint_still_works_when_a_different_cell_holds_a_wrong_value()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (wrongRow, wrongCol) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(wrongRow, wrongCol);
        svc.Place(TestGame.WrongValueFor(svc.Current, wrongRow, wrongCol));

        // Every other empty cell must still produce a hint.
        int checkedCells = 0;
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (svc.Current.Cells[r, c].IsGiven) continue;
            if (r == wrongRow && c == wrongCol) continue;
            if (svc.Current.Get(r, c) is not null) continue;

            svc.Select(r, c);
            var hint = svc.GetHintForSelectedCell();

            Assert.True(hint is not null, $"No hint offered for empty cell ({r},{c}).");
            Assert.Equal(svc.Current.SolutionAt(r, c), hint!.Value.value);
            checkedCells++;
        }

        Assert.True(checkedCells > 0, "Test did not examine any cells.");
    }

    // The old implementation skipped cells that already had a value, so asking for a
    // hint on a wrong entry just echoed that same wrong value back.
    [Fact]
    public void Hint_corrects_a_wrong_value_in_the_selected_cell()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var correct = svc.Current.SolutionAt(row, col)!.Value;
        var wrong = TestGame.WrongValueFor(svc.Current, row, col);

        svc.Select(row, col);
        svc.Place(wrong);

        var hint = svc.GetHintForSelectedCell();

        Assert.NotNull(hint);
        Assert.Equal(correct, hint!.Value.value);
        Assert.NotEqual(wrong, hint.Value.value);
    }

    [Fact]
    public void Applying_a_hint_overwrites_a_wrong_entry_with_the_correct_value()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var correct = svc.Current.SolutionAt(row, col)!.Value;

        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));
        svc.ApplyHintForSelectedCell();

        Assert.Equal(correct, svc.Current.Get(row, col));
    }

    [Fact]
    public void Hint_is_unavailable_for_given_cells()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var given = FirstGivenCell(svc);
        svc.Select(given.Row, given.Col);

        Assert.Null(svc.GetHintForSelectedCell());
    }

    [Fact]
    public void Hint_is_unavailable_when_nothing_is_selected()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        svc.ClearSelection();

        Assert.Null(svc.GetHintForSelectedCell());
    }

    [Fact]
    public void Hint_matches_the_recorded_solution_on_an_untouched_board()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Medium);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);

        var hint = svc.GetHintForSelectedCell();

        Assert.NotNull(hint);
        Assert.Equal(row, hint!.Value.pos.Row);
        Assert.Equal(col, hint.Value.pos.Col);
        Assert.Equal(svc.Current.SolutionAt(row, col), hint.Value.value);
    }

    private static (int Row, int Col) FirstGivenCell(Application.Services.SudokuService svc)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (svc.Current.Cells[r, c].IsGiven) return (r, c);

        throw new InvalidOperationException("Board has no given cells.");
    }
}
