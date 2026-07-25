using System.Numerics;
using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

// Replays human solving techniques from cheapest to dearest over a candidate grid.
// Each pass applies the cheapest technique that makes progress; the grade is the
// most expensive tier that was ever needed. If no implemented technique progresses,
// the puzzle is Advanced.
public sealed class PuzzleGrader : IPuzzleGrader
{
    private const int AllMask = 0b1111111110; // candidate bits 1..9

    private static readonly int[][] Units = BuildUnits(); // 9 rows + 9 cols + 9 boxes
    private static readonly int[][] Peers = BuildPeers(); // 20 peers per cell

    public TechniqueTier Grade(Board board)
    {
        var values = new int[81];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            values[r * 9 + c] = board.Get(r, c) ?? 0;

        var candidates = new int[81];
        for (int i = 0; i < 81; i++)
        {
            if (values[i] != 0) continue;
            int mask = AllMask;
            foreach (var p in Peers[i])
                if (values[p] != 0) mask &= ~Bit(values[p]);
            candidates[i] = mask;
        }

        var tier = TechniqueTier.Singles;
        while (true)
        {
            bool solved = true;
            for (int i = 0; i < 81; i++)
            {
                if (values[i] != 0) continue;
                solved = false;
                // An empty cell with no candidates means the position is broken;
                // logic cannot finish it, which is the definition of Advanced here.
                if (candidates[i] == 0) return TechniqueTier.Advanced;
            }
            if (solved) return tier;

            if (PlaceAnySingle(values, candidates)) continue;
            if (ApplyLockedCandidates(candidates))
            {
                tier = Max(tier, TechniqueTier.LockedCandidate);
                continue;
            }
            if (ApplyNakedPairs(candidates))
            {
                tier = Max(tier, TechniqueTier.Pair);
                continue;
            }
            return TechniqueTier.Advanced;
        }
    }

    private static int Bit(int v) => 1 << v;

    private static bool IsSingleBit(int m) => m != 0 && (m & (m - 1)) == 0;

    private static TechniqueTier Max(TechniqueTier a, TechniqueTier b) => a > b ? a : b;

    private static void Place(int[] values, int[] candidates, int i, int v)
    {
        values[i] = v;
        candidates[i] = 0;
        foreach (var p in Peers[i]) candidates[p] &= ~Bit(v);
    }

    private static bool PlaceAnySingle(int[] values, int[] candidates)
    {
        // Naked single: a cell with exactly one candidate left.
        for (int i = 0; i < 81; i++)
        {
            if (values[i] != 0) continue;
            int m = candidates[i];
            if (IsSingleBit(m))
            {
                Place(values, candidates, i, BitOperations.TrailingZeroCount(m));
                return true;
            }
        }

        // Hidden single: a digit with exactly one home left inside a unit.
        foreach (var unit in Units)
        {
            for (int v = 1; v <= 9; v++)
            {
                int spot = -1, count = 0;
                foreach (var i in unit)
                {
                    if (values[i] == 0 && (candidates[i] & Bit(v)) != 0)
                    {
                        spot = i;
                        if (++count > 1) break;
                    }
                }
                if (count == 1)
                {
                    Place(values, candidates, spot, v);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ApplyLockedCandidates(int[] candidates)
    {
        for (int v = 1; v <= 9; v++)
        {
            int bit = Bit(v);

            // Pointing: within a box the digit is confined to one row or column,
            // so it can be removed from that row or column outside the box.
            for (int b = 0; b < 9; b++)
            {
                int br = b / 3 * 3, bc = b % 3 * 3;
                int rows = 0, cols = 0, n = 0;
                for (int dr = 0; dr < 3; dr++)
                for (int dc = 0; dc < 3; dc++)
                {
                    if ((candidates[(br + dr) * 9 + bc + dc] & bit) == 0) continue;
                    n++;
                    rows |= 1 << dr;
                    cols |= 1 << dc;
                }
                if (n < 2) continue; // zero is nothing; one is a hidden single's job

                if (IsSingleBit(rows) &&
                    EliminateFromRow(candidates, br + BitOperations.TrailingZeroCount((uint)rows), bit, skipColFrom: bc))
                    return true;

                if (IsSingleBit(cols) &&
                    EliminateFromCol(candidates, bc + BitOperations.TrailingZeroCount((uint)cols), bit, skipRowFrom: br))
                    return true;
            }

            // Claiming: within a row (or column) the digit is confined to one box,
            // so it can be removed from the rest of that box.
            for (int r = 0; r < 9; r++)
            {
                int boxes = 0, n = 0;
                for (int c = 0; c < 9; c++)
                    if ((candidates[r * 9 + c] & bit) != 0) { n++; boxes |= 1 << (c / 3); }

                if (n >= 2 && IsSingleBit(boxes) &&
                    EliminateFromBox(candidates, r / 3 * 3, BitOperations.TrailingZeroCount((uint)boxes) * 3, bit, skipRow: r, skipCol: -1))
                    return true;
            }
            for (int c = 0; c < 9; c++)
            {
                int boxes = 0, n = 0;
                for (int r = 0; r < 9; r++)
                    if ((candidates[r * 9 + c] & bit) != 0) { n++; boxes |= 1 << (r / 3); }

                if (n >= 2 && IsSingleBit(boxes) &&
                    EliminateFromBox(candidates, BitOperations.TrailingZeroCount((uint)boxes) * 3, c / 3 * 3, bit, skipRow: -1, skipCol: c))
                    return true;
            }
        }

        return false;
    }

    private static bool EliminateFromRow(int[] candidates, int r, int bit, int skipColFrom)
    {
        bool changed = false;
        for (int c = 0; c < 9; c++)
        {
            if (c >= skipColFrom && c < skipColFrom + 3) continue;
            int i = r * 9 + c;
            if ((candidates[i] & bit) != 0) { candidates[i] &= ~bit; changed = true; }
        }
        return changed;
    }

    private static bool EliminateFromCol(int[] candidates, int c, int bit, int skipRowFrom)
    {
        bool changed = false;
        for (int r = 0; r < 9; r++)
        {
            if (r >= skipRowFrom && r < skipRowFrom + 3) continue;
            int i = r * 9 + c;
            if ((candidates[i] & bit) != 0) { candidates[i] &= ~bit; changed = true; }
        }
        return changed;
    }

    private static bool EliminateFromBox(int[] candidates, int br, int bc, int bit, int skipRow, int skipCol)
    {
        bool changed = false;
        for (int r = br; r < br + 3; r++)
        for (int c = bc; c < bc + 3; c++)
        {
            if (r == skipRow || c == skipCol) continue;
            int i = r * 9 + c;
            if ((candidates[i] & bit) != 0) { candidates[i] &= ~bit; changed = true; }
        }
        return changed;
    }

    private static bool ApplyNakedPairs(int[] candidates)
    {
        foreach (var unit in Units)
        {
            for (int a = 0; a < 9; a++)
            {
                int ia = unit[a], mask = candidates[ia];
                if (mask == 0 || BitOperations.PopCount((uint)mask) != 2) continue;

                for (int b = a + 1; b < 9; b++)
                {
                    if (candidates[unit[b]] != mask) continue;

                    // Two cells share the same two candidates, so those digits can
                    // go nowhere else in this unit.
                    bool changed = false;
                    foreach (var i in unit)
                    {
                        if (i == ia || i == unit[b]) continue;
                        if ((candidates[i] & mask) != 0) { candidates[i] &= ~mask; changed = true; }
                    }
                    if (changed) return true;
                }
            }
        }

        return false;
    }

    private static int[][] BuildUnits()
    {
        var units = new List<int[]>(27);
        for (int r = 0; r < 9; r++)
            units.Add(Enumerable.Range(0, 9).Select(c => r * 9 + c).ToArray());
        for (int c = 0; c < 9; c++)
            units.Add(Enumerable.Range(0, 9).Select(r => r * 9 + c).ToArray());
        for (int br = 0; br < 9; br += 3)
        for (int bc = 0; bc < 9; bc += 3)
        {
            var box = new int[9];
            int k = 0;
            for (int r = br; r < br + 3; r++)
            for (int c = bc; c < bc + 3; c++)
                box[k++] = r * 9 + c;
            units.Add(box);
        }
        return units.ToArray();
    }

    private static int[][] BuildPeers()
    {
        var peers = new int[81][];
        for (int i = 0; i < 81; i++)
        {
            var set = new HashSet<int>();
            foreach (var unit in Units)
            {
                if (!unit.Contains(i)) continue;
                foreach (var j in unit)
                    if (j != i) set.Add(j);
            }
            peers[i] = set.ToArray();
        }
        return peers;
    }
}
