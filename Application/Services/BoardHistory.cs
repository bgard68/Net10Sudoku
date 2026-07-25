using Sudoku.Domain;

namespace Sudoku.Application.Services;

// Undo/redo over full-board snapshots. Snapshots make compound actions - a
// placement that also swept pencil marks from peers - revert atomically,
// at the cost of a board clone per action, which is trivial for 81 cells.
public sealed class BoardHistory
{
    private readonly Stack<Board> _undo = new();
    private readonly Stack<Board> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    // Record the state that exists BEFORE a mutation. Any new action makes the
    // redo trail unreachable, so it is discarded.
    public void Record(Board current)
    {
        _undo.Push(current.Clone());
        _redo.Clear();
    }

    public Board? Undo(Board current)
    {
        if (_undo.Count == 0) return null;
        _redo.Push(current.Clone());
        return _undo.Pop();
    }

    public Board? Redo(Board current)
    {
        if (_redo.Count == 0) return null;
        _undo.Push(current.Clone());
        return _redo.Pop();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
