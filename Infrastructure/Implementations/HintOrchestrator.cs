using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Infrastructure.Implementations;

public class HintOrchestrator : IHintOrchestrator
{
    private readonly ISudokuHintProvider _provider;

    public HintOrchestrator(ISudokuHintProvider provider)
    {
        _provider = provider;
    }

    public (Position pos, int value)? GetNextHint(Board board) => _provider.GetNextHint(board);

    public (Position pos, int value)? GetHintForSelected(Board board, Position selected)
    {
        // Default behavior: solve board and return the correct value for selected position
        var copy = board.Clone();
        // We don't have solver here; attempt to find hint via provider first
        var hint = _provider.GetNextHint(board);
        if (hint is not null && hint.Value.pos.Row == selected.Row && hint.Value.pos.Col == selected.Col)
            return hint;
        return null;
    }
}
