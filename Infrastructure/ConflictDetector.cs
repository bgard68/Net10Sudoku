using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

public sealed class ConflictDetector : IConflictDetector
{
    private readonly ISudokuValidator _validator;

    public ConflictDetector(ISudokuValidator validator) => _validator = validator;

    public bool HasConflict(Board board, int row, int col)
    {
        var value = board.Get(row, col);
        if (value is null) return false;
        return !_validator.CanPlace(board, row, col, value.Value);
    }
}
