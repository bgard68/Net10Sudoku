using Microsoft.Extensions.DependencyInjection;
using Sudoku.Application.Interfaces;

namespace Sudoku.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISudokuValidator, SudokuValidator>();
        services.AddSingleton<ISudokuSolver, SudokuSolver>();
        services.AddSingleton<IPuzzleGrader, PuzzleGrader>();
        services.AddSingleton<ISudokuGenerator, SudokuGenerator>();
        services.AddSingleton<IConflictDetector, ConflictDetector>();
        return services;
    }
}
