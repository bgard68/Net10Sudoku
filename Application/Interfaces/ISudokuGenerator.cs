namespace Sudoku.Application.Interfaces;

using Sudoku.Domain;
using Sudoku.Application.Models;

public interface ISudokuGenerator
{
    Board Generate(Difficulty difficulty);
}
