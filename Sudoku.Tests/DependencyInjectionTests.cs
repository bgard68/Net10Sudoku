using Microsoft.Extensions.DependencyInjection;
using Sudoku.Application;
using Sudoku.Application.Interfaces;
using Sudoku.Application.Services;
using Sudoku.Infrastructure;
using Sudoku.Infrastructure.Grading;

namespace Sudoku.Tests;

// Integration tests for the composition root. They build the same container
// Program.cs builds (with in-memory stand-ins for the browser-storage and
// system-clock leaves) and assert the wiring contract: a registration removed
// or re-scoped by accident fails here instead of on the first request.
public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ResolvesIGameService_AsTheRealCoordinator()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var game = scope.ServiceProvider.GetRequiredService<IGameService>();

        Assert.IsType<SudokuService>(game);
    }

    [Fact]
    public void AddApplication_GameServiceIsScoped_SameInstancePerScopeDifferentAcrossScopes()
    {
        using var provider = BuildProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var firstResolve = scope1.ServiceProvider.GetRequiredService<IGameService>();
        var secondResolve = scope1.ServiceProvider.GetRequiredService<IGameService>();
        var otherScope = scope2.ServiceProvider.GetRequiredService<IGameService>();

        Assert.Same(firstResolve, secondResolve);
        Assert.NotSame(firstResolve, otherScope);
    }

    [Fact]
    public void AddInfrastructure_SolverIsSingleton_SharedAcrossScopes()
    {
        using var provider = BuildProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var solverInScope1 = scope1.ServiceProvider.GetRequiredService<ISudokuSolver>();
        var solverInScope2 = scope2.ServiceProvider.GetRequiredService<ISudokuSolver>();

        Assert.Same(solverInScope1, solverInScope2);
        Assert.IsType<SudokuSolver>(solverInScope1);
    }

    [Fact]
    public void AddInfrastructure_RegistersAllThreeGradingTechniques_CheapestFirst()
    {
        using var provider = BuildProvider();

        var techniques = provider.GetServices<IGradingTechnique>();

        Assert.Collection(techniques,
            t => Assert.IsType<SinglesTechnique>(t),
            t => Assert.IsType<LockedCandidatesTechnique>(t),
            t => Assert.IsType<NakedPairsTechnique>(t));
    }

    [Fact]
    public void GameSession_ResolvesFromAScope_WithItsDocumentedDefaults()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var session = scope.ServiceProvider.GetRequiredService<GameSession>();

        Assert.Equal(Sudoku.Application.Models.Difficulty.Easy, session.CurrentDifficulty);
        Assert.False(session.IsGenerating);
        Assert.False(session.IsSolved);
    }

    [Fact]
    public void FullyWiredGenerator_ProducesAPlayablePuzzleWithARecordedSolution()
    {
        using var provider = BuildProvider();

        var board = provider.GetRequiredService<ISudokuGenerator>()
            .Generate(Sudoku.Application.Models.Difficulty.Easy);

        Assert.True(board.HasSolution);
        Assert.True(provider.GetRequiredService<ISudokuValidator>().IsValid(board));
        Assert.False(provider.GetRequiredService<ISudokuValidator>().IsComplete(board));
    }

    // The exact service graph Program.cs composes, with test doubles only for
    // the two leaves that need a browser or a wall clock. ValidateOnBuild makes
    // an unconstructable registration fail at container build time.
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddScoped<IGameStore, MemoryGameStore>();
        services.AddSingleton<TimeProvider>(new TestClock());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
