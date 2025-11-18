namespace Sudoku.Domain;

public sealed class Cell
{
    public int Row { get; }
    public int Col { get; }
    public int? Value { get; private set; }
    public bool IsGiven { get; private set; }

    public Cell(int row, int col, int? value = null, bool given = false)
    {
        Row = row;
        Col = col;
        Value = value;
        IsGiven = given;
    }

    public void Set(int? value, bool given = false)
    {
        // Prevent modifying cells that are marked as given (puzzle clues).
        if (IsGiven && !given)
            throw new InvalidOperationException("Cannot modify a given cell.");

        Value = value;
        if (given) IsGiven = true;
    }
}
