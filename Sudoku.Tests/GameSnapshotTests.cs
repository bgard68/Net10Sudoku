using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Tests;

public class GameSnapshotTests
{
    [Fact]
    public void Snapshot_round_trips_values_givens_notes_and_solution()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);
        svc.ToggleNotesMode();
        var (row2, col2) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row2, col2);
        svc.Place(3);
        svc.Place(8);

        var snapshot = GameSnapshot.Capture(svc.Current, TimeSpan.FromSeconds(90), Difficulty.Medium);
        var restored = snapshot.ToBoard();

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            Assert.Equal(svc.Current.Get(r, c), restored.Get(r, c));
            Assert.Equal(svc.Current.Cells[r, c].IsGiven, restored.Cells[r, c].IsGiven);
            Assert.Equal(
                svc.Current.Cells[r, c].Notes.OrderBy(n => n),
                restored.Cells[r, c].Notes.OrderBy(n => n));
            Assert.Equal(svc.Current.SolutionAt(r, c), restored.SolutionAt(r, c));
        }

        Assert.Equal(90, snapshot.ElapsedSeconds);
        Assert.Equal(Difficulty.Medium, snapshot.Difficulty);
    }

    [Fact]
    public void Restored_board_still_answers_hints_from_the_recorded_solution()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var snapshot = GameSnapshot.Capture(svc.Current, TimeSpan.Zero, Difficulty.Easy);

        svc.Restore(snapshot.ToBoard());

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        var hint = svc.GetHintForSelectedCell();

        Assert.NotNull(hint);
        Assert.Equal(svc.Current.SolutionAt(row, col), hint!.Value.value);
    }

    [Fact]
    public void Restore_clears_undo_history()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);
        Assert.True(svc.CanUndo);

        var snapshot = GameSnapshot.Capture(svc.Current, TimeSpan.Zero, Difficulty.Easy);
        svc.Restore(snapshot.ToBoard());

        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void ToBoard_rejects_wrongly_sized_arrays()
    {
        var snapshot = new GameSnapshot
        {
            Values = new int[80],
            Givens = new bool[81],
            NoteMasks = new int[81]
        };

        Assert.Throws<ArgumentException>(() => snapshot.ToBoard());
    }

    [Fact]
    public void ToBoard_rejects_out_of_range_values()
    {
        var values = new int[81];
        values[0] = 12;

        var snapshot = new GameSnapshot
        {
            Values = values,
            Givens = new bool[81],
            NoteMasks = new int[81]
        };

        Assert.Throws<ArgumentException>(() => snapshot.ToBoard());
    }
}
