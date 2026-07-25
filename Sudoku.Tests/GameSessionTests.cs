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
    public async Task Initialize_with_an_empty_store_starts_a_fresh_easy_game()
    {
        var (session, _, _) = NewSession();

        await session.InitializeAsync();

        Assert.Equal(Difficulty.Easy, session.CurrentDifficulty);
        Assert.False(session.IsGenerating);
        Assert.Equal("New Easy puzzle generated.", session.Message);
    }

    [Fact]
    public async Task Initialize_restores_a_saved_game_with_its_clock_and_mistakes()
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
    public async Task Initialize_falls_back_to_a_new_game_when_the_snapshot_is_corrupt()
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
    public async Task Starting_a_game_resets_the_clock_and_record_flags_and_persists()
    {
        var (session, _, store) = NewSession();

        await session.StartNewAsync(Difficulty.Hard);

        Assert.Equal(Difficulty.Hard, session.CurrentDifficulty);
        Assert.Equal(TimeSpan.Zero, session.Elapsed);
        Assert.False(session.NewBest);
        Assert.False(session.UsedAutoSolve);
        Assert.NotNull(store.Game); // the fresh game is saved immediately
        Assert.Equal(Difficulty.Hard, store.Game!.Difficulty);
    }

    [Fact]
    public async Task The_clock_follows_the_time_provider()
    {
        var (session, _, _) = NewSession(out var clock);
        await session.StartNewAsync(Difficulty.Easy);

        clock.Advance(TimeSpan.FromSeconds(42));
        await session.TickAsync();

        Assert.Equal(TimeSpan.FromSeconds(42), session.Elapsed);
    }

    [Fact]
    public async Task The_running_game_is_persisted_every_twentieth_tick()
    {
        var (session, _, store) = NewSession(out var clock);
        await session.StartNewAsync(Difficulty.Easy);
        store.GameSaves = 0; // ignore the save from StartNewAsync

        for (int i = 0; i < 19; i++) await session.TickAsync();
        Assert.Equal(0, store.GameSaves);

        clock.Advance(TimeSpan.FromSeconds(10));
        await session.TickAsync(); // 20th
        Assert.Equal(1, store.GameSaves);
    }

    [Fact]
    public async Task A_genuine_win_sets_a_best_time()
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
    public async Task A_slower_win_keeps_the_existing_record()
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
    public async Task An_auto_solved_win_never_sets_a_record()
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
    public async Task Solving_clears_the_save_so_there_is_nothing_to_restore()
    {
        var (session, _, store) = NewSession();
        await session.StartNewAsync(Difficulty.Easy);
        Assert.NotNull(store.Game);

        session.MarkSolved();
        await session.PersistAsync();

        Assert.Null(store.Game);
    }

    [Fact]
    public async Task Undoing_past_a_win_reopens_the_game()
    {
        var (session, _, _) = NewSession();
        await session.StartNewAsync(Difficulty.Easy);

        session.MarkSolved();
        Assert.True(session.IsSolved);

        session.ResetSolved();
        Assert.False(session.IsSolved);
    }

    [Fact]
    public async Task Each_new_board_bumps_the_board_version()
    {
        var (session, _, _) = NewSession();
        await session.StartNewAsync(Difficulty.Easy);
        var first = session.BoardVersion;

        await session.StartNewAsync(Difficulty.Medium);

        Assert.True(session.BoardVersion > first);
    }

    private static (GameSession Session, IGameService Game, MemoryStore Store) NewSession()
        => NewSession(out _);

    private static (GameSession Session, IGameService Game, MemoryStore Store) NewSession(out TestClock clock)
    {
        var game = TestGame.Service();
        var store = new MemoryStore();
        clock = new TestClock();
        return (new GameSession(game, store, clock), game, store);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class MemoryStore : IGameStore
    {
        public GameSnapshot? Game;
        public int GameSaves;
        public readonly Dictionary<Difficulty, int> Bests = new();

        public Task<GameSnapshot?> LoadGameAsync() => Task.FromResult(Game);

        public Task SaveGameAsync(GameSnapshot snapshot)
        {
            Game = snapshot;
            GameSaves++;
            return Task.CompletedTask;
        }

        public Task ClearGameAsync()
        {
            Game = null;
            return Task.CompletedTask;
        }

        public Task<int?> LoadBestSecondsAsync(Difficulty difficulty) =>
            Task.FromResult(Bests.TryGetValue(difficulty, out var s) ? (int?)s : null);

        public Task SaveBestSecondsAsync(Difficulty difficulty, int seconds)
        {
            Bests[difficulty] = seconds;
            return Task.CompletedTask;
        }
    }
}
