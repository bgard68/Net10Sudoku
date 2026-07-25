using Microsoft.Extensions.DependencyInjection;
using Sudoku.Application.Interfaces;
using Sudoku.Application.Services;

namespace Sudoku.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped = per Blazor circuit, i.e. one game per connected player.
        services.AddScoped<IGameService, SudokuService>();
        return services;
    }
}
