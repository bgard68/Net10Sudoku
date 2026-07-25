using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Domain;
using Sudoku.Infrastructure.Grading;

namespace Sudoku.Infrastructure;

// Replays human solving techniques from cheapest to dearest over a candidate
// grid; the grade is the most expensive tier that was ever needed. The
// techniques are strategies - extending the grader means adding a technique
// class and registering it, not editing this loop.
public sealed class PuzzleGrader : IPuzzleGrader
{
    private readonly IGradingTechnique[] _techniques;

    public PuzzleGrader() : this(DefaultTechniques()) { }

    public PuzzleGrader(IEnumerable<IGradingTechnique> techniques)
    {
        // Cheapest first, so the grade reflects what the puzzle *demands*,
        // not merely what some expensive technique could also find.
        _techniques = techniques.OrderBy(t => t.Tier).ToArray();
        if (_techniques.Length == 0)
            throw new ArgumentException("At least one grading technique is required.", nameof(techniques));
    }

    public static IReadOnlyList<IGradingTechnique> DefaultTechniques() =>
    [
        new SinglesTechnique(),
        new LockedCandidatesTechnique(),
        new NakedPairsTechnique()
    ];

    public TechniqueTier Grade(Board board)
    {
        var grid = new GradingGrid(board);
        var tier = TechniqueTier.Singles;

        while (true)
        {
            switch (grid.Evaluate())
            {
                case GridState.Solved: return tier;
                case GridState.Broken: return TechniqueTier.Advanced;
            }

            var progressed = false;
            foreach (var technique in _techniques)
            {
                if (technique.Apply(grid))
                {
                    if (technique.Tier > tier) tier = technique.Tier;
                    progressed = true;
                    break; // restart from the cheapest technique
                }
            }

            if (!progressed) return TechniqueTier.Advanced;
        }
    }
}
