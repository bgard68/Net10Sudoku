using Sudoku.Domain;

namespace Sudoku.Application.Interfaces;

public interface IHintOrchestrator
{
    (Position pos, int value)? GetNextHint(Board board);
    (Position pos, int value)? GetHintForSelected(Board board, Position selected);
}
