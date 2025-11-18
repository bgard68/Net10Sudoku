namespace Sudoku.Application.Interfaces;

using Sudoku.Domain;

public interface IConflictDetector
{
    bool HasConflict(Board board, int row, int col);
}
