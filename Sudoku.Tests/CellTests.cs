using Sudoku.Domain;

namespace Sudoku.Tests;

// Unit tests for Cell behaviour. Cell mutators are internal by design, so every
// interaction goes through Board - the same path production code uses.
public class CellTests
{
    [Fact]
    public void ToggleNote_OnEmptyEditableCell_AddsTheNote()
    {
        var board = new Board();

        board.ToggleNote(3, 4, 7);

        Assert.True(board[3, 4].HasNote(7));
        Assert.Single(board[3, 4].Notes);
        Assert.Null(board[3, 4].Value);
    }

    [Fact]
    public void ToggleNote_SameValueTwice_RemovesTheNote()
    {
        var board = new Board();
        board.ToggleNote(3, 4, 7);

        board.ToggleNote(3, 4, 7);

        Assert.False(board[3, 4].HasNote(7));
        Assert.Empty(board[3, 4].Notes);
    }

    [Fact]
    public void ToggleNote_OnGivenCell_IsSilentlyIgnored()
    {
        var board = new Board();
        board.Set(2, 2, 5, given: true);

        board.ToggleNote(2, 2, 7);

        Assert.False(board[2, 2].HasNote(7));
        Assert.Empty(board[2, 2].Notes);
    }

    [Fact]
    public void ToggleNote_OnCellWithValue_IsSilentlyIgnored()
    {
        var board = new Board();
        board.Set(2, 2, 5);

        board.ToggleNote(2, 2, 7);

        Assert.False(board[2, 2].HasNote(7));
        Assert.Equal(5, board[2, 2].Value);
    }

    [Fact]
    public void Set_ValueOnCellWithNotes_ClearsEveryNote()
    {
        var board = new Board();
        board.ToggleNote(0, 0, 2);
        board.ToggleNote(0, 0, 9);

        board.Set(0, 0, 4);

        Assert.Equal(4, board[0, 0].Value);
        Assert.Empty(board[0, 0].Notes);
    }

    [Fact]
    public void Set_NullOnGivenCell_ThrowsInvalidOperationException()
    {
        var board = new Board();
        board.Set(5, 5, 8, given: true);

        var act = () => board.Set(5, 5, null);

        Assert.Throws<InvalidOperationException>(act);
        Assert.Equal(8, board[5, 5].Value);
    }

    [Fact]
    public void Set_GivenOverGiven_UpdatesValueAndStaysGiven()
    {
        // The generator's final marking pass re-sets given cells with given: true;
        // that path must remain legal while player writes stay forbidden.
        var board = new Board();
        board.Set(1, 1, 6, given: true);

        board.Set(1, 1, 7, given: true);

        Assert.Equal(7, board[1, 1].Value);
        Assert.True(board[1, 1].IsGiven);
    }

    [Fact]
    public void RemoveNote_ValuePresent_RemovesOnlyThatNote()
    {
        var board = new Board();
        board.ToggleNote(6, 6, 3);
        board.ToggleNote(6, 6, 7);

        board.RemoveNote(6, 6, 3);

        Assert.False(board[6, 6].HasNote(3));
        Assert.True(board[6, 6].HasNote(7));
        Assert.Single(board[6, 6].Notes);
    }

    [Fact]
    public void RemoveNote_ValueAbsent_LeavesNotesUnchanged()
    {
        var board = new Board();
        board.ToggleNote(6, 6, 7);

        board.RemoveNote(6, 6, 3);

        Assert.True(board[6, 6].HasNote(7));
        Assert.Single(board[6, 6].Notes);
    }

    [Fact]
    public void ClearNotes_CellWithSeveralNotes_RemovesThemAll()
    {
        var board = new Board();
        board.ToggleNote(8, 0, 1);
        board.ToggleNote(8, 0, 5);
        board.ToggleNote(8, 0, 9);

        board.ClearNotes(8, 0);

        Assert.Empty(board[8, 0].Notes);
    }

    [Fact]
    public void Constructor_AssignsRowAndColToTheCell()
    {
        var board = new Board();

        var cell = board[3, 7];

        Assert.Equal(3, cell.Row);
        Assert.Equal(7, cell.Col);
    }
}
