using Sudoku.Application.Models;

namespace Sudoku.Infrastructure.Grading;

// One human solving technique. The grader tries techniques from cheapest tier
// upward and records the dearest one it needed - so adding a technique means
// adding a class and registering it, never editing the grading algorithm.
public interface IGradingTechnique
{
    TechniqueTier Tier { get; }

    // Make one unit of progress (a placement or an elimination) if possible.
    // Returns false when this technique finds nothing on the current grid.
    bool Apply(GradingGrid grid);
}
