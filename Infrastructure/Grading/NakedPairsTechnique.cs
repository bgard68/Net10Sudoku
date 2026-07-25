using Sudoku.Application.Models;

namespace Sudoku.Infrastructure.Grading;

// Two cells in a unit share the same two candidates, so those digits can go
// nowhere else in that unit.
public sealed class NakedPairsTechnique : IGradingTechnique
{
    public TechniqueTier Tier => TechniqueTier.Pair;

    public bool Apply(GradingGrid grid)
    {
        foreach (var unit in GradingGrid.Units)
        {
            for (int a = 0; a < 9; a++)
            {
                int ia = unit[a], mask = grid.Candidates(ia);
                if (mask == 0 || GradingGrid.PopCount(mask) != 2) continue;

                for (int b = a + 1; b < 9; b++)
                {
                    if (grid.Candidates(unit[b]) != mask) continue;

                    bool changed = false;
                    foreach (var i in unit)
                    {
                        if (i == ia || i == unit[b]) continue;
                        changed |= grid.EliminateMask(i, mask);
                    }
                    if (changed) return true;
                }
            }
        }

        return false;
    }
}
