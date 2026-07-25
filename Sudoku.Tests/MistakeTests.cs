using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class MistakeTests
{
    [Fact]
    public void A_wrong_placement_counts_as_a_mistake()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));

        Assert.Equal(1, svc.Mistakes);
    }

    [Fact]
    public void A_correct_placement_is_not_a_mistake()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(svc.Current.SolutionAt(row, col)!.Value);

        Assert.Equal(0, svc.Mistakes);
    }

    [Fact]
    public void Pencil_notes_are_never_mistakes()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.ToggleNotesMode();
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));

        Assert.Equal(0, svc.Mistakes);
    }

    // The mistake happened; taking the move back does not unhappen it.
    [Fact]
    public void Undo_does_not_forgive_a_mistake()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));
        svc.Undo();

        Assert.Equal(1, svc.Mistakes);
    }

    [Fact]
    public void A_new_game_resets_the_count()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));
        Assert.Equal(1, svc.Mistakes);

        svc.New(Difficulty.Easy);
        Assert.Equal(0, svc.Mistakes);
    }

    [Fact]
    public void Mistakes_survive_a_snapshot_round_trip()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));

        var snapshot = GameSnapshot.Capture(svc.Current, TimeSpan.Zero, Difficulty.Easy, svc.Mistakes);
        svc.Restore(snapshot.ToBoard(), snapshot.Mistakes);

        Assert.Equal(1, svc.Mistakes);
    }
}
