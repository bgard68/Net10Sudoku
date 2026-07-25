namespace Sudoku.Application.Interfaces;

using Sudoku.Domain;

public interface ISudokuSolver
{
    // Completes the board in place; returns false when no solution exists
    // (including a board whose givens already conflict).
    bool TrySolve(Board board);

    // Counts solutions up to `limit` and stops there - uniqueness checking only
    // needs to know "0, 1, or more than 1", never the exact total. Does not
    // mutate the board.
    int CountSolutions(Board board, int limit);
}
