using System.Numerics;
using Sudoku.Application.Interfaces;
using Sudoku.Domain;

namespace Sudoku.Infrastructure;

// Backtracking solver over row/column/box bitmasks. Candidate lookup is three
// mask reads instead of 27 cell scans, and the search always branches on the
// most constrained cell first, which prunes the tree dramatically. Generation
// calls this once per dig for the uniqueness check, so the constant factor
// matters.
public sealed class SudokuSolver : ISudokuSolver
{
    private const int AllMask = 0b1111111110; // digit bits 1..9

    public bool TrySolve(Board board)
    {
        var state = State.Load(board);
        if (state is null) return false; // existing values already conflict

        if (!Solve(state)) return false;

        // Only a successful search writes back, so a failed attempt leaves the
        // caller's board untouched.
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            var cell = board.Cells[r, c];
            if (cell.Value is null) cell.Set(state.Values[r * 9 + c]);
        }
        return true;
    }

    public int CountSolutions(Board board, int limit)
    {
        if (limit <= 0) return 0;

        var state = State.Load(board);
        if (state is null) return 0;

        int count = 0;
        Count(state, limit, ref count);
        return count;
    }

    private sealed class State
    {
        public readonly int[] Values = new int[81];
        public readonly int[] RowMask = new int[9];
        public readonly int[] ColMask = new int[9];
        public readonly int[] BoxMask = new int[9];

        // Null when the board's existing values conflict with each other.
        public static State? Load(Board board)
        {
            var s = new State();
            for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                if (board.Get(r, c) is not int v) continue;
                int bit = 1 << v;
                int b = r / 3 * 3 + c / 3;
                if (((s.RowMask[r] | s.ColMask[c] | s.BoxMask[b]) & bit) != 0)
                    return null;
                s.Values[r * 9 + c] = v;
                s.RowMask[r] |= bit;
                s.ColMask[c] |= bit;
                s.BoxMask[b] |= bit;
            }
            return s;
        }

        // The empty cell with the fewest candidates, or -1 when the board is
        // full. A cell with zero or one candidates is taken immediately - zero
        // proves the branch dead, one is forced - so scanning further for
        // something "better" would be wasted work.
        public int MostConstrainedCell(out int candidates)
        {
            int best = -1, bestCount = 10;
            candidates = 0;
            for (int i = 0; i < 81; i++)
            {
                if (Values[i] != 0) continue;
                int m = CandidatesAt(i);
                int n = BitOperations.PopCount((uint)m);
                if (n >= bestCount) continue;
                best = i;
                bestCount = n;
                candidates = m;
                if (n <= 1) break;
            }
            return best;
        }

        public int CandidatesAt(int i) =>
            AllMask & ~(RowMask[i / 9] | ColMask[i % 9] | BoxMask[i / 9 / 3 * 3 + i % 9 / 3]);

        public void Place(int i, int v)
        {
            int bit = 1 << v;
            Values[i] = v;
            RowMask[i / 9] |= bit;
            ColMask[i % 9] |= bit;
            BoxMask[i / 9 / 3 * 3 + i % 9 / 3] |= bit;
        }

        public void Remove(int i, int v)
        {
            int bit = ~(1 << v);
            Values[i] = 0;
            RowMask[i / 9] &= bit;
            ColMask[i % 9] &= bit;
            BoxMask[i / 9 / 3 * 3 + i % 9 / 3] &= bit;
        }
    }

    private static bool Solve(State s)
    {
        int i = s.MostConstrainedCell(out int candidates);
        if (i < 0) return true;

        while (candidates != 0)
        {
            int v = BitOperations.TrailingZeroCount(candidates);
            s.Place(i, v);
            if (Solve(s)) return true;
            s.Remove(i, v);
            candidates &= candidates - 1;
        }
        return false;
    }

    private static void Count(State s, int limit, ref int count)
    {
        int i = s.MostConstrainedCell(out int candidates);
        if (i < 0)
        {
            count++;
            return;
        }

        while (candidates != 0 && count < limit)
        {
            int v = BitOperations.TrailingZeroCount(candidates);
            s.Place(i, v);
            Count(s, limit, ref count);
            s.Remove(i, v);
            candidates &= candidates - 1;
        }
    }
}
