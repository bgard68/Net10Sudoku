using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Application.Interfaces;

// The application boundary the UI depends on. Components never see the
// concrete coordinator, so game rules can be reworked - or faked in a
// component test - without touching the presentation layer.
public interface IGameService
{
    Board Current { get; }
    Position? Selected { get; }
    bool NotesMode { get; }
    int Mistakes { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }

    void New(Difficulty difficulty);
    Task NewAsync(Difficulty difficulty);
    void Restore(Board board, int mistakes = 0);

    void Select(int row, int col);
    void ClearSelection();
    void ToggleNotesMode();

    void Place(int value);
    void Clear();
    void ClearAll();
    void Undo();
    void Redo();

    bool Validate();
    bool IsComplete();
    bool Solve();
    bool HasConflict(int row, int col);

    (Position pos, int value)? GetHintForSelectedCell();
    void ApplyHintForSelectedCell();
}
