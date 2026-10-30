using System.Reflection;
using NetArchTest.Rules;
using Sudoku.Application.Services;
using Sudoku.Domain;
using Sudoku.Infrastructure;
using Sudoku.Services;

namespace Sudoku.Tests;

// Dependency-direction tripwires for the clean-architecture split. The project
// files already keep every reference pointing inward (Sudoku -> Infrastructure
// -> Application -> Domain; docs/architecture.md), but a project file is one
// careless <ProjectReference> away from saying otherwise. These tests read the
// compiled IL, so the first outward reach fails here and names the exact types
// that leaked instead of surfacing as a design-review comment months later.
//
// NetArchTest matches dependencies by namespace prefix, and the host shares
// the "Sudoku" root with every layer - a bare "Sudoku" ban would condemn each
// assembly for using itself. The rules therefore name the host's concrete
// namespaces, and the final fact closes the gap at the assembly level (which
// also covers host types outside those namespaces, like Program).
public class ArchitectureTests
{
    // One real type per project pins each compiled assembly under test, so a
    // rename or move breaks the test at compile time rather than silently
    // scanning nothing.
    private static readonly Assembly DomainAssembly = typeof(Board).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(SudokuService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(SudokuSolver).Assembly;
    private static readonly Assembly HostAssembly = typeof(GameStorage).Assembly;

    private static readonly string[] HostNamespaces = ["Sudoku.Components", "Sudoku.Services"];

    // What the inner layers must never see: the web stack, the JS-interop
    // escape hatch to the browser, and database clients. Microsoft.Extensions.*
    // is deliberately not on this list - Application and Infrastructure
    // register themselves through IServiceCollection, which is exactly why
    // both csproj files reference DependencyInjection.Abstractions on purpose.
    private static readonly string[] WebAndPersistenceRoots =
    [
        "Microsoft.AspNetCore",
        "Microsoft.JSInterop",
        "Microsoft.EntityFrameworkCore",
        "System.Data",
    ];

    // Board, Cell and Position are the enterprise core; a reference to any
    // sibling layer is a wrong-direction arrow by definition.
    [Fact]
    public void Domain_ReferencesToOtherLayers_AreAbsent()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(["Sudoku.Application", "Sudoku.Infrastructure", .. HostNamespaces])
            .GetResult();

        Assert.Empty(Offenders(result));
    }

    // The domain must not know it runs in a web app or that anything is ever
    // persisted - saving is the host's IGameStore adapter's business.
    [Fact]
    public void Domain_WebAndPersistenceDependencies_AreAbsent()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(WebAndPersistenceRoots)
            .GetResult();

        Assert.Empty(Offenders(result));
    }

    // Application defines the ports; the adapters that satisfy them live in
    // the outer rings. Reaching for Infrastructure, the host, or the web
    // stack would collapse ports-and-adapters into direct coupling.
    [Fact]
    public void Application_OutwardAndWebDependencies_AreAbsent()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(["Sudoku.Infrastructure", .. HostNamespaces, .. WebAndPersistenceRoots])
            .GetResult();

        Assert.Empty(Offenders(result));
    }

    // Infrastructure adapts the Application ports; only the composition root
    // in Program.cs may know both sides exist.
    [Fact]
    public void Infrastructure_ReferencesToTheHost_AreAbsent()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(HostNamespaces)
            .GetResult();

        Assert.Empty(Offenders(result));
    }

    // The namespace rules above cannot ban the bare "Sudoku" prefix, so this
    // fact pins the direction at the assembly level: the compiler records a
    // reference only when a type is actually used, and any outward edge shows
    // up here by name, e.g. "Application -> Infrastructure".
    [Fact]
    public void InnerAssemblies_CompiledReferences_PointOnlyInward()
    {
        var host = HostAssembly.GetName().Name ?? "Sudoku";
        (Assembly assembly, string[] banned)[] layers =
        [
            (DomainAssembly, ["Application", "Infrastructure", host]),
            (ApplicationAssembly, ["Infrastructure", host]),
            (InfrastructureAssembly, [host]),
        ];

        var offendingEdges = layers
            .SelectMany(layer => layer.assembly.GetReferencedAssemblies()
                .Where(reference => layer.banned.Contains(reference.Name ?? string.Empty))
                .Select(reference => $"{layer.assembly.GetName().Name} -> {reference.Name}"))
            .ToList();

        Assert.Empty(offendingEdges);
    }

    // ----- helpers ---------------------------------------------------------

    // A red architecture test must say which types leaked and what they
    // reached for, not just "IsSuccessful was false".
    private static List<string> Offenders(TestResult result)
        => (result.FailingTypes ?? [])
            .Select(t => string.IsNullOrEmpty(t.Explanation) ? t.FullName : $"{t.FullName} ({t.Explanation})")
            .ToList();
}
