using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;

namespace Sudoku.Tests;

// A clock the test moves by hand, so every timing assertion is exact.
internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

// In-memory IGameStore. Mirrors the port contract: never throws, and exposes
// its state so tests can assert exactly what was persisted.
internal sealed class MemoryGameStore : IGameStore
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
