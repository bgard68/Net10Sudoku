namespace Sudoku.Application.Interfaces;

using Sudoku.Domain;

public interface ISudokuHintProvider
{
    // Returns a position and value to place, or null if none.
    (Position pos, int value)? GetNextHint(Board board);
}
