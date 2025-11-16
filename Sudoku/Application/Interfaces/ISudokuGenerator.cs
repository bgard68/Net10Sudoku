namespace Sudoku.Application.Interfaces;

using Sudoku.Domain;

public interface ISudokuGenerator
{
    Board Generate(Difficulty difficulty);
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
    Professional
}
