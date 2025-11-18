using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Application.Implementations;

public class GameState : IGameState
{
    public Board Current { get; set; } = new();
    public Position? Selected { get; set; }
}
