using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Sudoku.Application.Models;

namespace Sudoku.Services;

// Persists the in-progress game and theme choice in the browser so a page
// refresh or circuit reset does not lose them. Storage failures are swallowed:
// persistence is best-effort and must never break gameplay.
public sealed class GameStorage
{
    private const string GameKey = "sudoku.game";
    private const string ThemeKey = "sudoku.darkmode";

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

    public async Task<bool?> LoadDarkModeAsync()
    {
        try
        {
            var result = await _storage.GetAsync<bool>(ThemeKey);
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveDarkModeAsync(bool darkMode)
    {
        try { await _storage.SetAsync(ThemeKey, darkMode); }
        catch { /* best effort */ }
    }
}
