namespace Sudoku.Domain;

public sealed class Board
{
    public Cell[,] Cells { get; }

    public Board()
    {
        Cells = new Cell[9,9];
        for (var r = 0; r < 9; r++)
        for (var c = 0; c < 9; c++)
            Cells[r,c] = new Cell(r,c);
    }

    public int? Get(int r, int c) => Cells[r,c].Value;
    public void Set(int r, int c, int? v, bool given = false) => Cells[r,c].Set(v, given);

    // Create a deep copy of the board (values and given flags)
    public Board Clone()
    {
        var copy = new Board();
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var v = Cells[r,c].Value;
            var given = Cells[r,c].IsGiven;
            copy.Cells[r,c].Set(v, given);
        }
        return copy;
    }
}
