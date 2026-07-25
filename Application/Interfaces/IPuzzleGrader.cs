using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Application.Interfaces;

public interface IPuzzleGrader
{
    // Rates how hard a puzzle is for a human by replaying logical techniques from
    // cheapest to dearest until the board is solved. Advanced means the implemented
    // techniques were not enough. Clue count alone cannot answer this question -
    // a 26-clue board can still fall to singles.
    TechniqueTier Grade(Board board);
}
