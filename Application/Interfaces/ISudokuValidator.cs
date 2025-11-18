namespace Sudoku.Application.Interfaces;

using Sudoku.Domain;

public interface ISudokuValidator
{
    bool IsValid(Board board);
    bool IsComplete(Board board);
    bool CanPlace(Board board, int row, int col, int value);
}
