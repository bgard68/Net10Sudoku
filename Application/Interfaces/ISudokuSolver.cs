namespace Sudoku.Application.Interfaces;

using Sudoku.Domain;

public interface ISudokuSolver
{
    bool TrySolve(Board board);
}
