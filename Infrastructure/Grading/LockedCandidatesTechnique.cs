using Sudoku.Application.Models;

namespace Sudoku.Infrastructure.Grading;

// Pointing: within a box a digit is confined to one row or column, so it can be
// removed from that row or column outside the box. Claiming: within a row or
// column the digit is confined to one box, so it can be removed from the rest
// of that box.
public sealed class LockedCandidatesTechnique : IGradingTechnique
{
    public TechniqueTier Tier => TechniqueTier.LockedCandidate;

    public bool Apply(GradingGrid grid)
    {
        for (int v = 1; v <= 9; v++)
        {
            int bit = GradingGrid.Bit(v);

            // Pointing
            for (int b = 0; b < 9; b++)
            {
                int br = b / 3 * 3, bc = b % 3 * 3;
                int rows = 0, cols = 0, n = 0;
                for (int dr = 0; dr < 3; dr++)
                for (int dc = 0; dc < 3; dc++)
                {
                    if ((grid.Candidates((br + dr) * 9 + bc + dc) & bit) == 0) continue;
                    n++;
                    rows |= 1 << dr;
                    cols |= 1 << dc;
                }
                if (n < 2) continue; // zero is nothing; one is a hidden single's job

                if (GradingGrid.IsSingleBit(rows) &&
                    EliminateFromRow(grid, br + GradingGrid.LowestDigit(rows), bit, skipColFrom: bc))
                    return true;

                if (GradingGrid.IsSingleBit(cols) &&
                    EliminateFromCol(grid, bc + GradingGrid.LowestDigit(cols), bit, skipRowFrom: br))
                    return true;
            }

            // Claiming
            for (int r = 0; r < 9; r++)
            {
                int boxes = 0, n = 0;
                for (int c = 0; c < 9; c++)
                    if ((grid.Candidates(r * 9 + c) & bit) != 0) { n++; boxes |= 1 << (c / 3); }

                if (n >= 2 && GradingGrid.IsSingleBit(boxes) &&
                    EliminateFromBox(grid, r / 3 * 3, GradingGrid.LowestDigit(boxes) * 3, bit, skipRow: r, skipCol: -1))
                    return true;
            }
            for (int c = 0; c < 9; c++)
            {
                int boxes = 0, n = 0;
                for (int r = 0; r < 9; r++)
                    if ((grid.Candidates(r * 9 + c) & bit) != 0) { n++; boxes |= 1 << (r / 3); }

                if (n >= 2 && GradingGrid.IsSingleBit(boxes) &&
                    EliminateFromBox(grid, GradingGrid.LowestDigit(boxes) * 3, c / 3 * 3, bit, skipRow: -1, skipCol: c))
                    return true;
            }
        }

        return false;
    }

    private static bool EliminateFromRow(GradingGrid grid, int r, int bit, int skipColFrom)
    {
        bool changed = false;
        for (int c = 0; c < 9; c++)
        {
            if (c >= skipColFrom && c < skipColFrom + 3) continue;
            changed |= grid.EliminateMask(r * 9 + c, bit);
        }
        return changed;
    }

    private static bool EliminateFromCol(GradingGrid grid, int c, int bit, int skipRowFrom)
    {
        bool changed = false;
        for (int r = 0; r < 9; r++)
        {
            if (r >= skipRowFrom && r < skipRowFrom + 3) continue;
            changed |= grid.EliminateMask(r * 9 + c, bit);
        }
        return changed;
    }

    private static bool EliminateFromBox(GradingGrid grid, int br, int bc, int bit, int skipRow, int skipCol)
    {
        bool changed = false;
        for (int r = br; r < br + 3; r++)
        for (int c = bc; c < bc + 3; c++)
        {
            if (r == skipRow || c == skipCol) continue;
            changed |= grid.EliminateMask(r * 9 + c, bit);
        }
        return changed;
    }
}
