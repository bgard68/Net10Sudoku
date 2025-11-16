namespace Sudoku.Domain;

public readonly record struct Position(int Row, int Col)
{
    public bool IsValid => Row >= 0 && Row < 9 && Col >= 0 && Col < 9;
}
