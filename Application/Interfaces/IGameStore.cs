using Sudoku.Application.Models;

namespace Sudoku.Application.Interfaces;

// Port for persisting the in-progress game and per-difficulty best times.
// The web host implements it over protected browser storage; tests implement
// it in memory. All operations are best-effort: implementations must swallow
// storage failures rather than break gameplay.
public interface IGameStore
{
    Task<GameSnapshot?> LoadGameAsync();
    Task SaveGameAsync(GameSnapshot snapshot);
    Task ClearGameAsync();

    Task<int?> LoadBestSecondsAsync(Difficulty difficulty);
    Task SaveBestSecondsAsync(Difficulty difficulty, int seconds);
}
