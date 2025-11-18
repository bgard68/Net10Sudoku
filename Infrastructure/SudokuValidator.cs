using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

public sealed class SudokuValidator : ISudokuValidator
{
    public bool IsValid(Board board)
    {
        for (int r = 0; r < 9; r++)
        {
            var row = new bool[10];
            for (int c = 0; c < 9; c++)
            {
                var v = board.Get(r,c);
                if (v is null) continue;
                if (row[v.Value]) return false;
                row[v.Value] = true;
            }
        }
        for (int c = 0; c < 9; c++)
        {
            var col = new bool[10];
            for (int r = 0; r < 9; r++)
            {
                var v = board.Get(r,c);
                if (v is null) continue;
                if (col[v.Value]) return false;
                col[v.Value] = true;
            }
        }
        for (int br = 0; br < 3; br++)
        for (int bc = 0; bc < 3; bc++)
        {
            var box = new bool[10];
            for (int r = br*3; r < br*3+3; r++)
            for (int c = bc*3; c < bc*3+3; c++)
            {
                var v = board.Get(r,c);
                if (v is null) continue;
                if (box[v.Value]) return false;
                box[v.Value] = true;
            }
        }
        return true;
    }

    public bool IsComplete(Board board)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (board.Get(r,c) is null) return false;
        return IsValid(board);
    }

    public bool CanPlace(Board board, int row, int col, int value)
    {
        for (int c = 0; c < 9; c++)
        {
            if (c == col) continue;
            if (board.Get(row,c) == value) return false;
        }
        for (int r = 0; r < 9; r++)
        {
            if (r == row) continue;
            if (board.Get(r,col) == value) return false;
        }
        int br = (row/3)*3, bc = (col/3)*3;
        for (int r = br; r < br+3; r++)
        for (int c = bc; c < bc+3; c++)
        {
            if (r == row && c == col) continue;
            if (board.Get(r,c) == value) return false;
        }
        return true;
    }
}
