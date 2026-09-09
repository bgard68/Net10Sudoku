using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Sudoku.Application.Models;
using Sudoku.Services;

namespace Sudoku.Tests;

// Integration tests for the persistence adapter. They run the real
// ProtectedLocalStorage and real data-protection stack; only the JavaScript
// boundary is faked with an in-memory localStorage, so serialization,
// encryption and the adapter's swallow-all-failures contract are all genuine.
public class GameStorageTests
{
    private readonly FakeJsLocalStorage _js = new();
    private readonly GameStorage _storage;

    public GameStorageTests()
    {
        var protectedStorage = new ProtectedLocalStorage(_js, new EphemeralDataProtectionProvider());
        _storage = new GameStorage(protectedStorage);
    }

    [Fact]
    public void Constructor_NullStorage_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new GameStorage(null!));

        Assert.Equal("storage", ex.ParamName);
    }

    [Fact]
    public async Task SaveGameAsync_ThenLoadGameAsync_RoundTripsTheFullSnapshot()
    {
        var board = TestBoards.Puzzle();
        board.ToggleNote(0, 0, 3);
        board.ToggleNote(0, 0, 7);
        var snapshot = GameSnapshot.Capture(board, TimeSpan.FromSeconds(90), Difficulty.Medium, mistakes: 2);

        await _storage.SaveGameAsync(snapshot);
        var loaded = await _storage.LoadGameAsync();

        Assert.Equal(snapshot.Values, loaded!.Values);
        Assert.Equal(snapshot.Givens, loaded.Givens);
        Assert.Equal(snapshot.NoteMasks, loaded.NoteMasks);
        Assert.Equal(snapshot.Solution, loaded.Solution);
        Assert.Equal(Difficulty.Medium, loaded.Difficulty);
        Assert.Equal(90, loaded.ElapsedSeconds);
        Assert.Equal(2, loaded.Mistakes);
        Assert.Equal((1 << 3) | (1 << 7), loaded.NoteMasks[0]);
    }

    [Fact]
    public async Task SaveGameAsync_PayloadAtRest_IsProtectedNotPlainJson()
    {
        var snapshot = GameSnapshot.Capture(TestBoards.Puzzle(), TimeSpan.Zero, Difficulty.Easy);

        await _storage.SaveGameAsync(snapshot);

        var atRest = _js.Store["sudoku.game"];
        Assert.DoesNotContain("Values", atRest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Givens", atRest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadGameAsync_NothingStored_ReturnsNull()
    {
        var loaded = await _storage.LoadGameAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadGameAsync_TamperedPayload_ReturnsNullInsteadOfThrowing()
    {
        await _storage.SaveGameAsync(GameSnapshot.Capture(TestBoards.Puzzle(), TimeSpan.Zero, Difficulty.Easy));
        _js.Store["sudoku.game"] = "definitely-not-a-protected-payload";

        var loaded = await _storage.LoadGameAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadGameAsync_JsInteropFailure_ReturnsNullInsteadOfThrowing()
    {
        _js.FailNextCall = true;

        var loaded = await _storage.LoadGameAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveGameAsync_JsInteropFailure_IsSwallowedAndStoresNothing()
    {
        _js.FailNextCall = true;

        await _storage.SaveGameAsync(GameSnapshot.Capture(TestBoards.Puzzle(), TimeSpan.Zero, Difficulty.Easy));

        Assert.False(_js.Store.ContainsKey("sudoku.game"));
    }

    [Fact]
    public async Task ClearGameAsync_RemovesTheSavedGame()
    {
        await _storage.SaveGameAsync(GameSnapshot.Capture(TestBoards.Puzzle(), TimeSpan.Zero, Difficulty.Easy));

        await _storage.ClearGameAsync();
        var loaded = await _storage.LoadGameAsync();

        Assert.Null(loaded);
        Assert.False(_js.Store.ContainsKey("sudoku.game"));
    }

    [Fact]
    public async Task ClearGameAsync_JsInteropFailure_IsSwallowed()
    {
        await _storage.SaveGameAsync(GameSnapshot.Capture(TestBoards.Puzzle(), TimeSpan.Zero, Difficulty.Easy));
        _js.FailNextCall = true;

        await _storage.ClearGameAsync();

        Assert.True(_js.Store.ContainsKey("sudoku.game")); // the delete failed quietly
    }

    [Fact]
    public async Task SaveBestSecondsAsync_KeysEachDifficultySeparately()
    {
        // The key scheme is a storage contract: renaming it would orphan every
        // player's saved best times on the next deploy.
        await _storage.SaveBestSecondsAsync(Difficulty.Easy, 90);
        await _storage.SaveBestSecondsAsync(Difficulty.Medium, 120);

        Assert.True(_js.Store.ContainsKey("sudoku.best.Easy"));
        Assert.True(_js.Store.ContainsKey("sudoku.best.Medium"));
    }

    [Fact]
    public async Task LoadBestSecondsAsync_RoundTripsWithoutCrossTalkBetweenDifficulties()
    {
        await _storage.SaveBestSecondsAsync(Difficulty.Easy, 90);
        await _storage.SaveBestSecondsAsync(Difficulty.Medium, 120);

        var easy = await _storage.LoadBestSecondsAsync(Difficulty.Easy);
        var medium = await _storage.LoadBestSecondsAsync(Difficulty.Medium);
        var hard = await _storage.LoadBestSecondsAsync(Difficulty.Hard);

        Assert.Equal(90, easy);
        Assert.Equal(120, medium);
        Assert.Null(hard);
    }

    [Fact]
    public async Task LoadBestSecondsAsync_TamperedPayload_ReturnsNull()
    {
        await _storage.SaveBestSecondsAsync(Difficulty.Easy, 90);
        _js.Store["sudoku.best.Easy"] = "garbage";

        var best = await _storage.LoadBestSecondsAsync(Difficulty.Easy);

        Assert.Null(best);
    }

    // In-memory stand-in for the browser's localStorage JS interop surface.
    private sealed class FakeJsLocalStorage : IJSRuntime
    {
        public readonly Dictionary<string, string> Store = new(StringComparer.Ordinal);
        public bool FailNextCall;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (FailNextCall)
            {
                FailNextCall = false;
                throw new JSException("Simulated interop failure.");
            }

            switch (identifier)
            {
                case "localStorage.setItem":
                    Store[(string)args![0]!] = (string)args[1]!;
                    return ValueTask.FromResult(default(TValue)!);

                case "localStorage.getItem":
                    Store.TryGetValue((string)args![0]!, out var stored);
                    return ValueTask.FromResult((TValue)(object?)stored!);

                case "localStorage.removeItem":
                    Store.Remove((string)args![0]!);
                    return ValueTask.FromResult(default(TValue)!);

                default:
                    throw new NotSupportedException($"Unexpected JS call: {identifier}");
            }
        }
    }
}
