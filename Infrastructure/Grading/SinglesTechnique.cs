using Sudoku.Application.Models;

namespace Sudoku.Infrastructure.Grading;

// Naked single: a cell with exactly one candidate left.
// Hidden single: a digit with exactly one home left inside a unit.
public sealed class SinglesTechnique : IGradingTechnique
{
    public TechniqueTier Tier => TechniqueTier.Singles;

    public bool Apply(GradingGrid grid)
    {
        for (int i = 0; i < 81; i++)
        {
            if (grid.Value(i) != 0) continue;
            int m = grid.Candidates(i);
            if (GradingGrid.IsSingleBit(m))
            {
                grid.Place(i, GradingGrid.LowestDigit(m));
                return true;
            }
        }

        foreach (var unit in GradingGrid.Units)
        {
            for (int v = 1; v <= 9; v++)
            {
                int spot = -1, count = 0;
                foreach (var i in unit)
                {
                    if (grid.Value(i) == 0 && grid.HasCandidate(i, v))
                    {
                        spot = i;
                        if (++count > 1) break;
                    }
                }
                if (count == 1)
                {
                    grid.Place(spot, v);
                    return true;
                }
            }
        }

        return false;
    }
}
