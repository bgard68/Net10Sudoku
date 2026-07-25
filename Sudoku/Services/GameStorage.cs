using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;

namespace Sudoku.Services;

// Adapter for the IGameStore port over protected browser storage. Storage
// failures are swallowed: persistence is best-effort and must never break
// gameplay.
public sealed class GameStorage : IGameStore
{
    private const string GameKey = "sudoku.game";

    private readonly ProtectedLocalStorage _storage;

    public GameStorage(ProtectedLocalStorage storage) => _storage = storage;

    public async Task<GameSnapshot?> LoadGameAsync()
    {
        try
        {
            var result = await _storage.GetAsync<GameSnapshot>(GameKey);
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveGameAsync(GameSnapshot snapshot)
    {
        try { await _storage.SetAsync(GameKey, snapshot); }
        catch { /* best effort */ }
    }

    public async Task ClearGameAsync()
    {
        try { await _storage.DeleteAsync(GameKey); }
        catch { /* best effort */ }
    }

    public async Task<int?> LoadBestSecondsAsync(Difficulty difficulty)
    {
        try
        {
            var result = await _storage.GetAsync<int>(BestKey(difficulty));
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveBestSecondsAsync(Difficulty difficulty, int seconds)
    {
        try { await _storage.SetAsync(BestKey(difficulty), seconds); }
        catch { /* best effort */ }
    }

    private static string BestKey(Difficulty difficulty) => $"sudoku.best.{difficulty}";
}
