using Microsoft.Extensions.DependencyInjection;
using Sudoku.Application.Interfaces;
using Sudoku.Infrastructure.Grading;

namespace Sudoku.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISudokuValidator, SudokuValidator>();
        services.AddSingleton<ISudokuSolver, SudokuSolver>();
        // Each grading technique is a strategy; adding one here is the only
        // change needed to extend the grader.
        services.AddSingleton<IGradingTechnique, SinglesTechnique>();
        services.AddSingleton<IGradingTechnique, LockedCandidatesTechnique>();
        services.AddSingleton<IGradingTechnique, NakedPairsTechnique>();
        services.AddSingleton<IPuzzleGrader, PuzzleGrader>();
        services.AddSingleton<ISudokuGenerator, SudokuGenerator>();
        services.AddSingleton<IConflictDetector, ConflictDetector>();
        return services;
    }
}
