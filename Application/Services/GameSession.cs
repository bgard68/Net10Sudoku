using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;

namespace Sudoku.Application.Services;

// Orchestrates the flow around a game: starting and restoring puzzles,
// persisting progress, the running clock, and best-time records. Extracted
// from the page component so every rule here - "auto-solve never sets a
// record", "a refresh loses at most ten seconds of clock" - is a fast unit
// test instead of a browser session. The page reduces to markup and event
// forwarding.
public sealed class GameSession
{
    // Ticks arrive ~every 500ms; persisting on every 20th bounds refresh loss to ~10s.
    private const int PersistEveryNthTick = 20;

    private readonly IGameService _game;
    private readonly IGameStore _store;
    private readonly TimeProvider _clock;

    private DateTimeOffset _startedUtc;
    private int _ticks;

    public GameSession(IGameService game, IGameStore store, TimeProvider clock)
    {
        _game = game;
        _store = store;
        _clock = clock;
        _startedUtc = clock.GetUtcNow();
    }

    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Easy;
    public bool IsGenerating { get; private set; }
    public bool IsSolved { get; private set; }
    public TimeSpan Elapsed { get; private set; }
    public TimeSpan? BestTime { get; private set; }
    public bool NewBest { get; private set; }
    public bool UsedAutoSolve { get; private set; }

    // Incremented whenever a different board is adopted; the UI keys the grid
    // element on it so a new puzzle re-creates the DOM subtree.
    public int BoardVersion { get; private set; }

    // Status line shown under the board. The UI also writes hint/validate
    // feedback here, so it is deliberately settable.
    public string? Message { get; set; }

    // Restore a saved game when one exists, otherwise start a fresh Easy puzzle.
    public async Task InitializeAsync()
    {
        var saved = await _store.LoadGameAsync();
        if (saved is not null && TryRestore(saved))
        {
            BestTime = ToTime(await _store.LoadBestSecondsAsync(CurrentDifficulty));
            return;
        }

        await StartNewAsync(Difficulty.Easy);
    }

    public async Task StartNewAsync(Difficulty difficulty)
    {
        if (IsGenerating) return;
        IsGenerating = true;
        IsSolved = false;
        Message = $"Generating {difficulty} puzzle...";

        await _game.NewAsync(difficulty);

        IsGenerating = false;
        CurrentDifficulty = difficulty;
        BoardVersion++;
        _startedUtc = _clock.GetUtcNow();
        Elapsed = TimeSpan.Zero;
        NewBest = false;
        UsedAutoSolve = false;
        Message = $"New {difficulty} puzzle generated.";

        await PersistAsync();
        BestTime = ToTime(await _store.LoadBestSecondsAsync(difficulty));
    }

    // Advance the clock. Roughly every ten seconds the running game is also
    // persisted, so a refresh loses at most that much progress on the timer.
    public Task TickAsync()
    {
        if (IsSolved || IsGenerating) return Task.CompletedTask;
        Elapsed = _clock.GetUtcNow() - _startedUtc;
        if (++_ticks % PersistEveryNthTick == 0) return PersistAsync();
        return Task.CompletedTask;
    }

    // A solved game clears its save - there is nothing to come back to.
    public Task PersistAsync() => IsSolved
        ? _store.ClearGameAsync()
        : _store.SaveGameAsync(GameSnapshot.Capture(_game.Current, Elapsed, CurrentDifficulty, _game.Mistakes));

    // The board is complete. Auto-solved games never set records - the record
    // belongs to the player.
    public void MarkSolved()
    {
        IsSolved = true;
        Message = null;
        Elapsed = _clock.GetUtcNow() - _startedUtc;

        if (!UsedAutoSolve && (BestTime is null || Elapsed < BestTime))
        {
            NewBest = true;
            BestTime = Elapsed;
            _ = _store.SaveBestSecondsAsync(CurrentDifficulty, (int)Elapsed.TotalSeconds);
        }
    }

    // Clearing or undoing can re-open a board that looked finished.
    public void ResetSolved() => IsSolved = false;

    public void MarkAutoSolveUsed() => UsedAutoSolve = true;

    private bool TryRestore(GameSnapshot saved)
    {
        try
        {
            // ToBoard throws on malformed data; stored state is outside our
            // control, so any failure just means "no saved game".
            var board = saved.ToBoard();
            _game.Restore(board, saved.Mistakes);
            CurrentDifficulty = saved.Difficulty;
            Elapsed = TimeSpan.FromSeconds(Math.Max(0, saved.ElapsedSeconds));
            _startedUtc = _clock.GetUtcNow() - Elapsed;
            IsSolved = false;
            NewBest = false;
            UsedAutoSolve = false;
            BoardVersion++;
            Message = "Restored your saved game.";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TimeSpan? ToTime(int? seconds) =>
        seconds is int s ? TimeSpan.FromSeconds(s) : null;
}
