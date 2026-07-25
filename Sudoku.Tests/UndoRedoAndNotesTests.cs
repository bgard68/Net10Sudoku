using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class UndoRedoAndNotesTests
{
    [Fact]
    public void Undo_reverts_a_placement_and_redo_restores_it()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);

        Assert.True(svc.CanUndo);
        svc.Undo();
        Assert.Null(svc.Current.Get(row, col));

        Assert.True(svc.CanRedo);
        svc.Redo();
        Assert.Equal(5, svc.Current.Get(row, col));
    }

    [Fact]
    public void Undo_reverts_ClearAll_in_one_step()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);
        svc.ClearAll();
        Assert.Null(svc.Current.Get(row, col));

        svc.Undo();
        Assert.Equal(5, svc.Current.Get(row, col));
    }

    [Fact]
    public void A_new_action_discards_the_redo_history()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);
        svc.Undo();
        Assert.True(svc.CanRedo);

        svc.Place(6);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void Starting_a_new_game_clears_history()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);
        Assert.True(svc.CanUndo);

        svc.New(Difficulty.Easy);
        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public async Task NewAsync_produces_a_playable_board_and_clears_history()
    {
        var svc = TestGame.Service();
        await svc.NewAsync(Difficulty.Easy);

        Assert.True(svc.Current.HasSolution);
        Assert.False(svc.CanUndo);
        Assert.True(svc.Validate());
    }

    [Fact]
    public void Notes_mode_toggles_pencil_marks_instead_of_placing()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.ToggleNotesMode();

        svc.Place(3);
        Assert.Null(svc.Current.Get(row, col));
        Assert.True(svc.Current.Cells[row, col].HasNote(3));

        svc.Place(3);
        Assert.False(svc.Current.Cells[row, col].HasNote(3));
    }

    [Fact]
    public void Placing_a_value_sweeps_that_note_from_peers()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col, otherCol) = RowWithTwoEmptyCells(svc.Current);

        svc.ToggleNotesMode();
        svc.Select(row, otherCol);
        svc.Place(7); // pencil in 7
        Assert.True(svc.Current.Cells[row, otherCol].HasNote(7));

        svc.ToggleNotesMode();
        svc.Select(row, col);
        svc.Place(7); // real placement of the same digit in the same row

        Assert.False(svc.Current.Cells[row, otherCol].HasNote(7));
    }

    [Fact]
    public void Undo_restores_swept_notes()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col, otherCol) = RowWithTwoEmptyCells(svc.Current);

        svc.ToggleNotesMode();
        svc.Select(row, otherCol);
        svc.Place(7);
        svc.ToggleNotesMode();
        svc.Select(row, col);
        svc.Place(7);
        Assert.False(svc.Current.Cells[row, otherCol].HasNote(7));

        svc.Undo();

        Assert.Null(svc.Current.Get(row, col));
        Assert.True(svc.Current.Cells[row, otherCol].HasNote(7));
    }

    // A 41-given Easy board occasionally leaves the first empty cell alone in its
    // row, so scan for any row that has two empties rather than assuming one.
    private static (int Row, int Col, int OtherCol) RowWithTwoEmptyCells(Domain.Board board)
    {
        for (int r = 0; r < 9; r++)
        {
            int first = -1;
            for (int c = 0; c < 9; c++)
            {
                if (board.Get(r, c) is not null) continue;
                if (first < 0) { first = c; continue; }
                return (r, first, c);
            }
        }

        throw new InvalidOperationException("No row with two empty cells found.");
    }

    [Fact]
    public void Clear_removes_notes_as_well_as_values()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.ToggleNotesMode();
        svc.Place(2);
        svc.Place(9);
        Assert.Equal(2, svc.Current.Cells[row, col].Notes.Count);

        svc.Clear();
        Assert.Empty(svc.Current.Cells[row, col].Notes);
    }

    [Fact]
    public void Placing_a_real_value_clears_the_cells_own_notes()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.ToggleNotesMode();
        svc.Place(2);
        svc.ToggleNotesMode();
        svc.Place(5);

        Assert.Equal(5, svc.Current.Get(row, col));
        Assert.Empty(svc.Current.Cells[row, col].Notes);
    }
}
