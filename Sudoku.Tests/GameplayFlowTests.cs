using Sudoku.Application.Models;
using Sudoku.Application.Services;

namespace Sudoku.Tests;

// Functional end-to-end workflows over the real service graph: generate, play,
// persist, restore in a fresh "circuit", and win - the journeys a player
// actually takes, asserted on exact business outcomes.
public class GameplayFlowTests
{
    [Fact]
    public void CompleteGame_EveryEntryCorrect_EndsCompleteValidAndMistakeFree()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);

        TestBoards.FillFromSolution(svc);

        Assert.True(svc.IsComplete());
        Assert.True(svc.Validate());
        Assert.Equal(0, svc.Mistakes);
        Assert.False(svc.HasConflict(0, 0));
    }

    [Fact]
    public void CompleteGame_WrongEntryCorrectedByHint_EndsCompleteWithTheMistakeOnRecord()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var wrong = TestGame.WrongValueFor(svc.Current, row, col);
        svc.Select(row, col);
        svc.Place(wrong);
        Assert.Equal(1, svc.Mistakes);

        svc.ApplyHintForSelectedCell();
        TestBoards.FillFromSolution(svc);

        Assert.True(svc.IsComplete());
        Assert.Equal(svc.Current.SolutionAt(row, col), svc.Current.Get(row, col));
        Assert.Equal(1, svc.Mistakes); // corrected, not forgiven
    }

    [Fact]
    public async Task Session_PersistThenRestoreInAFreshCircuit_ContinuesTheSameBoardClockAndMistakes()
    {
        var store = new MemoryGameStore();
        var clock = new TestClock();
        var firstCircuit = TestGame.Service();
        var firstSession = new GameSession(firstCircuit, store, clock);
        await firstSession.StartNewAsync(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(firstCircuit.Current);
        var wrong = TestGame.WrongValueFor(firstCircuit.Current, row, col);
        firstCircuit.Select(row, col);
        firstCircuit.Place(wrong);
        clock.Advance(TimeSpan.FromSeconds(75));
        await firstSession.TickAsync();
        await firstSession.PersistAsync();

        var secondCircuit = TestGame.Service();
        var secondSession = new GameSession(secondCircuit, store, clock);
        await secondSession.InitializeAsync();

        Assert.Equal(Difficulty.Easy, secondSession.CurrentDifficulty);
        Assert.Equal(TimeSpan.FromSeconds(75), secondSession.Elapsed);
        Assert.Equal(1, secondCircuit.Mistakes);
        Assert.Equal(wrong, secondCircuit.Current.Get(row, col));
        Assert.Equal("Restored your saved game.", secondSession.Message);
    }

    [Fact]
    public async Task Session_WinAfterARestore_SetsTheBestTimeAndClearsTheSave()
    {
        var store = new MemoryGameStore();
        var clock = new TestClock();
        var firstCircuit = TestGame.Service();
        var firstSession = new GameSession(firstCircuit, store, clock);
        await firstSession.StartNewAsync(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(firstCircuit.Current);
        firstCircuit.Select(row, col);
        firstCircuit.Place(TestGame.WrongValueFor(firstCircuit.Current, row, col));
        clock.Advance(TimeSpan.FromSeconds(75));
        await firstSession.TickAsync();
        await firstSession.PersistAsync();
        var secondCircuit = TestGame.Service();
        var secondSession = new GameSession(secondCircuit, store, clock);
        await secondSession.InitializeAsync();

        secondCircuit.Select(row, col);
        secondCircuit.ApplyHintForSelectedCell(); // repair the restored mistake
        TestBoards.FillFromSolution(secondCircuit);
        clock.Advance(TimeSpan.FromSeconds(25));
        secondSession.MarkSolved();
        await secondSession.PersistAsync();

        Assert.True(secondCircuit.IsComplete());
        Assert.True(secondSession.NewBest);
        Assert.Equal(TimeSpan.FromSeconds(100), secondSession.BestTime); // 75 restored + 25 played
        Assert.Equal(100, store.Bests[Difficulty.Easy]);
        Assert.Null(store.Game); // a finished game leaves nothing to restore
        Assert.Equal(1, secondCircuit.Mistakes);
    }
}
