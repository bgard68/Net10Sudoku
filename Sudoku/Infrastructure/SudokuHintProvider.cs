using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

public sealed class SudokuHintProvider : ISudokuHintProvider
{
    private readonly ISudokuValidator _validator;

    public SudokuHintProvider(ISudokuValidator validator)
    {
        _validator = validator;
    }

    public (Position pos, int value)? GetNextHint(Board board)
    {
        // Simple single-candidate hint: if a cell has only one possible value, suggest it.
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (board.Get(r,c) is not null) continue;
            var candidates = new List<int>(9);
            for (int v = 1; v <= 9; v++)
                if (_validator.CanPlace(board, r, c, v)) candidates.Add(v);
            if (candidates.Count == 1)
                return (new Position(r,c), candidates[0]);
        }
        // Fallback: try row/col/box singletons (hidden singles)
        for (int v = 1; v <= 9; v++)
        {
            // Rows
            for (int r = 0; r < 9; r++)
            {
                var spots = new List<Position>();
                for (int c = 0; c < 9; c++)
                    if (board.Get(r,c) is null && _validator.CanPlace(board,r,c,v)) spots.Add(new Position(r,c));
                if (spots.Count == 1) return (spots[0], v);
            }
            // Cols
            for (int c = 0; c < 9; c++)
            {
                var spots = new List<Position>();
                for (int r = 0; r < 9; r++)
                    if (board.Get(r,c) is null && _validator.CanPlace(board,r,c,v)) spots.Add(new Position(r,c));
                if (spots.Count == 1) return (spots[0], v);
            }
            // Boxes
            for (int br = 0; br < 3; br++)
            for (int bc = 0; bc < 3; bc++)
            {
                var spots = new List<Position>();
                for (int r = br*3; r < br*3+3; r++)
                for (int c = bc*3; c < bc*3+3; c++)
                    if (board.Get(r,c) is null && _validator.CanPlace(board,r,c,v)) spots.Add(new Position(r,c));
                if (spots.Count == 1) return (spots[0], v);
            }
        }
        return null;
    }
}
