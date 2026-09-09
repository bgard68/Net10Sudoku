using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class MistakeTests
{
    [Fact]
    public void Place_WrongValue_CountsOneMistake()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));

        Assert.Equal(1, svc.Mistakes);
    }

    [Fact]
    public void Place_CorrectValue_CountsNoMistake()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(svc.Current.SolutionAt(row, col)!.Value);

        Assert.Equal(0, svc.Mistakes);
    }

    [Fact]
    public void Place_WrongValueInNotesMode_CountsNoMistake()
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
    public void Undo_AfterAWrongPlacement_KeepsTheMistakeOnRecord()
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
    public void New_AfterMistakesWereMade_ResetsTheCountToZero()
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
    public void Restore_FromASnapshot_CarriesTheMistakeCountAcross()
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
