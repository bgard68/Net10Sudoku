using System.Numerics;
using Sudoku.Domain;

namespace Sudoku.Infrastructure.Grading;

public enum GridState
{
    InProgress,
    Solved,
    // An empty cell with no candidates: logic cannot finish this position.
    Broken
}

// The candidate model techniques operate on: 81 values plus a 9-bit candidate
// mask per empty cell, with the 27 units and 20-peer lists precomputed.
// Techniques read candidates and report progress through Place/EliminateMask;
// they never touch the domain Board directly.
public sealed class GradingGrid
{
    public const int AllMask = 0b1111111110; // digit bits 1..9

    private static readonly int[][] UnitList = BuildUnits(); // 9 rows + 9 cols + 9 boxes
    private static readonly int[][] PeerList = BuildPeers(); // 20 peers per cell

    public static IReadOnlyList<int[]> Units => UnitList;

    private readonly int[] _values = new int[81];
    private readonly int[] _candidates = new int[81];

    public GradingGrid(Board board)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            _values[r * 9 + c] = board.Get(r, c) ?? 0;

        for (int i = 0; i < 81; i++)
        {
            if (_values[i] != 0) continue;
            int mask = AllMask;
            foreach (var p in PeerList[i])
                if (_values[p] != 0) mask &= ~Bit(_values[p]);
            _candidates[i] = mask;
        }
    }

    public int Value(int i) => _values[i];

    public int Candidates(int i) => _candidates[i];

    public bool HasCandidate(int i, int v) => (_candidates[i] & Bit(v)) != 0;

    public void Place(int i, int v)
    {
        _values[i] = v;
        _candidates[i] = 0;
        foreach (var p in PeerList[i]) _candidates[p] &= ~Bit(v);
    }

    // Remove the masked digits from a cell's candidates; true when anything changed.
    public bool EliminateMask(int i, int mask)
    {
        if ((_candidates[i] & mask) == 0) return false;
        _candidates[i] &= ~mask;
        return true;
    }

    public GridState Evaluate()
    {
        var state = GridState.Solved;
        for (int i = 0; i < 81; i++)
        {
            if (_values[i] != 0) continue;
            if (_candidates[i] == 0) return GridState.Broken;
            state = GridState.InProgress;
        }
        return state;
    }

    public static int Bit(int v) => 1 << v;

    public static bool IsSingleBit(int m) => m != 0 && (m & (m - 1)) == 0;

    public static int PopCount(int m) => BitOperations.PopCount((uint)m);

    public static int LowestDigit(int m) => BitOperations.TrailingZeroCount(m);

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
            foreach (var unit in UnitList)
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
