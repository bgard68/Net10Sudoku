using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Application.Services;

namespace Sudoku.Tests;

// The game-flow rules that previously lived in the page component and could
// only be exercised by driving a browser: restore-or-new, persistence timing,
// clock resume, and best-time records.
public class GameSessionTests
{
    [Fact]
    public async Task InitializeAsync_EmptyStore_StartsAFreshEasyGame()
    {
        var (session, _, _) = NewSession();

        await session.InitializeAsync();

        Assert.Equal(Difficulty.Easy, session.CurrentDifficulty);
        Assert.False(session.IsGenerating);
        Assert.Equal("New Easy puzzle generated.", session.Message);
    }

    [Fact]
    public async Task InitializeAsync_SavedGame_RestoresItsBoardClockAndMistakes()
    {
        var (session, game, store) = NewSession();

        // Play a little, snapshot it into the store, then simulate a fresh circuit.
        game.New(Difficulty.Medium);
        var (row, col) = TestGame.FirstEmptyCell(game.Current);
        game.Select(row, col);
        game.Place(TestGame.WrongValueFor(game.Current, row, col));
        store.Game = GameSnapshot.Capture(game.Current, TimeSpan.FromSeconds(150), Difficulty.Medium, game.Mistakes);

        await session.InitializeAsync();

        Assert.Equal(Difficulty.Medium, session.CurrentDifficulty);
        Assert.Equal(TimeSpan.FromSeconds(150), session.Elapsed);
        Assert.Equal(1, game.Mistakes);
        Assert.Equal("Restored your saved game.", session.Message);
    }

    [Fact]
    public async Task InitializeAsync_CorruptSnapshot_FallsBackToANewEasyGame()
    {
        var (session, _, store) = NewSession();
        store.Game = new GameSnapshot
        {
            Values = new int[3], // wrong size - must be rejected
            Givens = new bool[81],
            NoteMasks = new int[81]
        };

        await session.InitializeAsync();

        Assert.Equal(Difficulty.Easy, session.CurrentDifficulty);
        Assert.Equal("New Easy puzzle generated.", session.Message);
    }

    [Fact]
    public async Task StartNewAsync_AnyDifficulty_ResetsTheClockAndRecordFlagsAndPersists()
    {
        var (session, _, store) = NewSession();

        await session.StartNewAsync(Difficulty.Hard);

        Assert.Equal(Difficulty.Hard, session.CurrentDifficulty);
        Assert.Equal(TimeSpan.Zero, session.Elapsed);
        Assert.False(session.NewBest);
        Assert.False(session.UsedAutoSolve);
        Assert.Equal(Difficulty.Hard, store.Game!.Difficulty); // the fresh game is saved immediately
    }

    [Fact]
    public async Task TickAsync_TimeAdvances_ElapsedFollowsTheTimeProvider()
    {
        var (session, _, _) = NewSession(out var clock);
        await session.StartNewAsync(Difficulty.Easy);

        clock.Advance(TimeSpan.FromSeconds(42));
        await session.TickAsync();

        Assert.Equal(TimeSpan.FromSeconds(42), session.Elapsed);
    }

    [Fact]
    public async Task TickAsync_NineteenTicks_DoesNotPersistYet()
    {
        var (session, _, store) = NewSession();
        await session.StartNewAsync(Difficulty.Easy);
        store.GameSaves = 0; // ignore the save from StartNewAsync

        await Tick(session, 19);

        Assert.Equal(0, store.GameSaves);
    }

    [Fact]
    public async Task TickAsync_EveryTwentiethTick_PersistsTheRunningGame()
    {
        var (session, _, store) = NewSession(out var clock);
        await session.StartNewAsync(Difficulty.Easy);
        store.GameSaves = 0;
        await Tick(session, 19);

        clock.Advance(TimeSpan.FromSeconds(10));
        await session.TickAsync(); // 20th

        Assert.Equal(1, store.GameSaves);
    }

    [Fact]
    public async Task MarkSolved_GenuineWin_SetsANewBestTime()
    {
        var (session, _, store) = NewSession(out var clock);
        await session.StartNewAsync(Difficulty.Easy);

        clock.Advance(TimeSpan.FromSeconds(90));
        session.MarkSolved();

        Assert.True(session.NewBest);
        Assert.Equal(TimeSpan.FromSeconds(90), session.BestTime);
        Assert.Equal(90, store.Bests[Difficulty.Easy]);
    }

    [Fact]
    public async Task MarkSolved_SlowerThanTheRecord_KeepsTheExistingRecord()
    {
        var (session, _, store) = NewSession(out var clock);
        store.Bests[Difficulty.Easy] = 60;
        await session.StartNewAsync(Difficulty.Easy);

        clock.Advance(TimeSpan.FromSeconds(90));
        session.MarkSolved();

        Assert.False(session.NewBest);
        Assert.Equal(TimeSpan.FromSeconds(60), session.BestTime);
        Assert.Equal(60, store.Bests[Difficulty.Easy]);
    }

    [Fact]
    public async Task MarkSolved_AfterAutoSolve_NeverSetsARecord()
    {
        var (session, _, store) = NewSession(out var clock);
        await session.StartNewAsync(Difficulty.Easy);

        session.MarkAutoSolveUsed();
        clock.Advance(TimeSpan.FromSeconds(5)); // absurdly fast, would smash any record
        session.MarkSolved();

        Assert.False(session.NewBest);
        Assert.False(store.Bests.ContainsKey(Difficulty.Easy));
    }

    [Fact]
    public async Task PersistAsync_SolvedGame_ClearsTheSaveSoThereIsNothingToRestore()
    {
        var (session, _, store) = NewSession();
        await session.StartNewAsync(Difficulty.Easy);
        Assert.NotNull(store.Game);

        session.MarkSolved();
        await session.PersistAsync();

        Assert.Null(store.Game);
    }

    [Fact]
    public async Task ResetSolved_AfterAWin_ReopensTheGame()
    {
        var (session, _, _) = NewSession();
        await session.StartNewAsync(Difficulty.Easy);
        session.MarkSolved();
        Assert.True(session.IsSolved);

        session.ResetSolved();

        Assert.False(session.IsSolved);
    }

    [Fact]
    public async Task StartNewAsync_EachNewBoard_BumpsTheBoardVersion()
    {
        var (session, _, _) = NewSession();
        await session.StartNewAsync(Difficulty.Easy);
        var first = session.BoardVersion;

        await session.StartNewAsync(Difficulty.Medium);

        Assert.Equal(first + 1, session.BoardVersion);
    }

    // Repeated ticking lives in a helper so the test bodies stay loop-free.
    private static async Task Tick(GameSession session, int times)
    {
        for (int i = 0; i < times; i++) await session.TickAsync();
    }

    private static (GameSession Session, IGameService Game, MemoryGameStore Store) NewSession()
        => NewSession(out _);

    private static (GameSession Session, IGameService Game, MemoryGameStore Store) NewSession(out TestClock clock)
    {
        var game = TestGame.Service();
        var store = new MemoryGameStore();
        clock = new TestClock();
        return (new GameSession(game, store, clock), game, store);
    }
}
