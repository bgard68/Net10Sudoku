using Sudoku.Application.Services;
using Sudoku.Domain;

namespace Sudoku.Tests;

// BoardHistory in isolation: the snapshot-stack semantics every undo/redo
// feature in the service builds on.
public class BoardHistoryTests
{
    [Fact]
    public void CanUndoAndCanRedo_NewHistory_AreBothFalse()
    {
        var history = new BoardHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Undo_EmptyHistory_ReturnsNullAndLeavesRedoEmpty()
    {
        var history = new BoardHistory();
        var board = new Board();

        var result = history.Undo(board);

        Assert.Null(result);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Redo_EmptyHistory_ReturnsNullAndLeavesUndoEmpty()
    {
        var history = new BoardHistory();
        var board = new Board();

        var result = history.Redo(board);

        Assert.Null(result);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Undo_AfterRecordAndMutation_ReturnsTheStateBeforeTheMutation()
    {
        var history = new BoardHistory();
        var board = new Board();
        board.Set(0, 0, 5);
        history.Record(board);
        board.Set(0, 0, 7);

        var previous = history.Undo(board);

        Assert.Equal(5, previous!.Get(0, 0));
        Assert.Equal(7, board.Get(0, 0));
    }

    [Fact]
    public void Undo_ThenRedo_RestoresTheUndoneState()
    {
        var history = new BoardHistory();
        var board = new Board();
        board.Set(0, 0, 5);
        history.Record(board);
        board.Set(0, 0, 7);

        var previous = history.Undo(board)!;
        var next = history.Redo(previous);

        Assert.Equal(7, next!.Get(0, 0));
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void Record_AfterAnUndo_DiscardsTheRedoTrail()
    {
        var history = new BoardHistory();
        var board = new Board();
        history.Record(board);
        history.Undo(board);
        Assert.True(history.CanRedo);

        history.Record(board);

        Assert.False(history.CanRedo);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void Record_TakesASnapshotNotAReference_SoLaterEditsDoNotRewriteHistory()
    {
        var history = new BoardHistory();
        var board = new Board();
        board.ToggleNote(1, 1, 3);
        history.Record(board);

        board.ToggleNote(1, 1, 3); // remove the note after recording
        var previous = history.Undo(board);

        Assert.True(previous![1, 1].HasNote(3));
    }

    [Fact]
    public void Clear_PopulatedHistory_EmptiesBothTrails()
    {
        var history = new BoardHistory();
        var board = new Board();
        history.Record(board);
        history.Undo(board);
        history.Record(board);

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }
}
