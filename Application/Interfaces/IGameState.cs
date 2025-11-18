using Sudoku.Domain;

namespace Sudoku.Application.Interfaces;

public interface IGameState
{
    Board Current { get; set; }
    Position? Selected { get; set; }
}
