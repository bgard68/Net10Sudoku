using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Application.Services;
using Sudoku.Domain;

namespace Sudoku.Tests;

// Edge rules around the session flow that the happy-path suite does not reach:
// reentrancy while generation is in flight, clock freezing, record ties, and
// hostile snapshot values. A hand-rolled IGameService whose generation blocks
// on a TaskCompletionSource makes the in-flight window observable.
public class GameSessionEdgeTests
{
    [Fact]
    public void Constructor_NullArguments_ThrowArgumentNullExceptionNamingTheParameter()
    {
        var game = new PendingGameService();
        var store = new MemoryGameStore();
        var clock = new TestClock();

        var noGame = Assert.Throws<ArgumentNullException>(() => new GameSession(null!, store, clock));
        var noStore = Assert.Throws<ArgumentNullException>(() => new GameSession(game, null!, clock));
        var noClock = Assert.Throws<ArgumentNullException>(() => new GameSession(game, store, null!));

        Assert.Equal("game", noGame.ParamName);
        Assert.Equal("store", noStore.ParamName);
        Assert.Equal("clock", noClock.ParamName);
    }

    [Fact]
    public async Task StartNewAsync_WhileGenerationIsInFlight_IgnoresTheSecondRequest()
    {
        var game = new PendingGameService();
        var session = new GameSession(game, new MemoryGameStore(), new TestClock());

        var first = session.StartNewAsync(Difficulty.Easy);
        var second = session.StartNewAsync(Difficulty.Hard);

        Assert.True(second.IsCompletedSuccessfully); // rejected without generating
        Assert.True(session.IsGenerating);
        Assert.Equal(1, game.GenerateCalls);

        game.CompleteGeneration();
        await first;

        Assert.False(session.IsGenerating);
        Assert.Equal(Difficulty.Easy, session.CurrentDifficulty);
        Assert.Equal("New Easy puzzle generated.", session.Message);
    }

    [Fact]
    public async Task TickAsync_WhileGenerationIsInFlight_DoesNotAdvanceTheClock()
    {
        var game = new PendingGameService();
        var clock = new TestClock();
        var session = new GameSession(game, new MemoryGameStore(), clock);
        var pending = session.StartNewAsync(Difficulty.Easy);

        clock.Advance(TimeSpan.FromSeconds(5));
        await session.TickAsync();

        Assert.Equal(TimeSpan.Zero, session.Elapsed);

        game.CompleteGeneration();
        await pending;
    }

    [Fact]
    public async Task TickAsync_AfterTheGameIsSolved_FreezesElapsedTime()
    {
        var (session, _, _, clock) = RealSession();
        await session.StartNewAsync(Difficulty.Easy);
        clock.Advance(TimeSpan.FromSeconds(30));
        await session.TickAsync();
        session.MarkSolved();

        clock.Advance(TimeSpan.FromSeconds(10));
        await session.TickAsync();

        Assert.Equal(TimeSpan.FromSeconds(30), session.Elapsed);
    }

    [Fact]
    public async Task MarkSolved_TimeExactlyEqualToTheRecord_KeepsTheExistingRecord()
    {
        var (session, _, store, clock) = RealSession();
        store.Bests[Difficulty.Easy] = 90;
        await session.StartNewAsync(Difficulty.Easy);

        clock.Advance(TimeSpan.FromSeconds(90));
        session.MarkSolved();

        Assert.False(session.NewBest);
        Assert.Equal(TimeSpan.FromSeconds(90), session.BestTime);
        Assert.Equal(90, store.Bests[Difficulty.Easy]);
    }

    [Fact]
    public async Task InitializeAsync_RestoredGame_LoadsTheBestTimeForThatDifficulty()
    {
        var (session, _, store, _) = RealSession();
        store.Game = GameSnapshot.Capture(TestBoards.Puzzle(), TimeSpan.FromSeconds(10), Difficulty.Medium);
        store.Bests[Difficulty.Medium] = 200;

        await session.InitializeAsync();

        Assert.Equal(Difficulty.Medium, session.CurrentDifficulty);
        Assert.Equal(TimeSpan.FromSeconds(200), session.BestTime);
    }

    [Fact]
    public async Task InitializeAsync_SnapshotWithNegativeElapsedSeconds_ClampsTheClockToZero()
    {
        var (session, _, store, _) = RealSession();
        store.Game = GameSnapshot.Capture(TestBoards.Puzzle(), TimeSpan.Zero, Difficulty.Easy)
            with { ElapsedSeconds = -50 };

        await session.InitializeAsync();

        Assert.Equal(TimeSpan.Zero, session.Elapsed);
        Assert.Equal("Restored your saved game.", session.Message);
    }

    [Fact]
    public async Task PersistAsync_ActiveGame_WritesTheExactBoardMistakesAndDifficulty()
    {
        var (session, game, store, _) = RealSession();
        await session.StartNewAsync(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(game.Current);
        var wrong = TestGame.WrongValueFor(game.Current, row, col);
        game.Select(row, col);
        game.Place(wrong);

        await session.PersistAsync();

        Assert.Equal(wrong, store.Game!.Values[row * 9 + col]);
        Assert.Equal(1, store.Game.Mistakes);
        Assert.Equal(Difficulty.Easy, store.Game.Difficulty);
    }

    // ----- fixtures ------------------------------------------------------

    private static (GameSession Session, IGameService Game, MemoryGameStore Store, TestClock Clock) RealSession()
    {
        var game = TestGame.Service();
        var store = new MemoryGameStore();
        var clock = new TestClock();
        return (new GameSession(game, store, clock), game, store, clock);
    }

    // Generation blocks until the test releases it, exposing the IsGenerating
    // window that a real (fast) generator closes before it can be observed.
    private sealed class PendingGameService : IGameService
    {
        private readonly TaskCompletionSource _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int GenerateCalls { get; private set; }
        public Board Current { get; private set; } = new();
        public Position? Selected => null;
        public bool NotesMode => false;
        public int Mistakes => 0;
        public bool CanUndo => false;
        public bool CanRedo => false;

        public void CompleteGeneration() => _pending.TrySetResult();

        public void New(Difficulty difficulty) => GenerateCalls++;

        public async Task NewAsync(Difficulty difficulty)
        {
            GenerateCalls++;
            await _pending.Task;
            Current = TestBoards.Puzzle();
        }

        public void Restore(Board board, int mistakes = 0) => Current = board;

        public void Select(int row, int col) { }
        public void ClearSelection() { }
        public void ToggleNotesMode() { }
        public void Place(int value) { }
        public void Clear() { }
        public void ClearAll() { }
        public void Undo() { }
        public void Redo() { }
        public bool Validate() => true;
        public bool IsComplete() => false;
        public bool Solve() => false;
        public bool HasConflict(int row, int col) => false;
        public (Position pos, int value)? GetHintForSelectedCell() => null;
        public void ApplyHintForSelectedCell() { }
    }
}
