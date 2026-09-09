using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class UndoRedoAndNotesTests
{
    [Fact]
    public void Undo_AfterAPlacement_ClearsTheCellAndRedoRestoresIt()
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
    public void Undo_AfterClearAll_RevertsEveryClearedCellInOneStep()
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
    public void Place_AfterAnUndo_DiscardsTheRedoHistory()
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
    public void New_AfterMovesWereMade_ClearsBothHistoryTrails()
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
    public async Task NewAsync_FreshGame_ProducesAPlayableBoardWithNoHistory()
    {
        var svc = TestGame.Service();

        await svc.NewAsync(Difficulty.Easy);

        Assert.True(svc.Current.HasSolution);
        Assert.False(svc.CanUndo);
        Assert.True(svc.Validate());
    }

    [Fact]
    public void Place_InNotesMode_TogglesAPencilMarkInsteadOfPlacing()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.ToggleNotesMode();

        svc.Place(3);
        Assert.Null(svc.Current.Get(row, col));
        Assert.True(svc.Current[row, col].HasNote(3));

        svc.Place(3);
        Assert.False(svc.Current[row, col].HasNote(3));
    }

    [Fact]
    public void Place_RealValue_SweepsThatPencilMarkFromItsPeers()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col, otherCol) = RowWithTwoEmptyCells(svc.Current);
        svc.ToggleNotesMode();
        svc.Select(row, otherCol);
        svc.Place(7);
        Assert.True(svc.Current[row, otherCol].HasNote(7));

        svc.ToggleNotesMode();
        svc.Select(row, col);
        svc.Place(7);

        Assert.False(svc.Current[row, otherCol].HasNote(7));
    }

    [Fact]
    public void Undo_AfterAPlacementThatSweptNotes_RestoresThoseNotes()
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
        Assert.False(svc.Current[row, otherCol].HasNote(7));

        svc.Undo();

        Assert.Null(svc.Current.Get(row, col));
        Assert.True(svc.Current[row, otherCol].HasNote(7));
    }

    [Fact]
    public void Clear_CellHoldingNotes_RemovesThemAlongWithAnyValue()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.ToggleNotesMode();
        svc.Place(2);
        svc.Place(9);
        Assert.Equal(2, svc.Current[row, col].Notes.Count);

        svc.Clear();

        Assert.Empty(svc.Current[row, col].Notes);
    }

    [Fact]
    public void Place_RealValueOverACellWithNotes_ClearsThatCellsOwnNotes()
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
        Assert.Empty(svc.Current[row, col].Notes);
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
}
