using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

public sealed class SudokuSolver : ISudokuSolver
{
    private readonly ISudokuValidator _validator;
    public SudokuSolver(ISudokuValidator validator) => _validator = validator;

    public bool TrySolve(Board board)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (board.Get(r,c) is not null) continue;
            for (int v = 1; v <= 9; v++)
            {
                if (!_validator.CanPlace(board, r, c, v)) continue;
                board.Set(r,c,v);
                if (TrySolve(board)) return true;
                board.Set(r,c,null);
            }
            return false;
        }
        return _validator.IsValid(board);
    }
}
