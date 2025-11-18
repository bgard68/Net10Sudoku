using Microsoft.Extensions.DependencyInjection;
using Sudoku.Application.Services;
using Sudoku.Application.Interfaces;
using Sudoku.Application.Implementations;

namespace Sudoku.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SudokuService>();
        services.AddScoped<IGameState, GameState>();
        return services;
    }
}
