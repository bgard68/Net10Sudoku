using Sudoku.Application.Models;
using Sudoku.Domain;
using Sudoku.Infrastructure;
using Sudoku.Infrastructure.Grading;

namespace Sudoku.Tests;

// Unit tests for the candidate grid and each grading technique, on hand-built
// positions where the expected deduction is verifiable on paper. The existing
// grader tests work on generated puzzles; these pin the mechanics.
public class GradingGridTests
{
    [Fact]
    public void Constructor_NullBoard_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new GradingGrid(null!));

        Assert.Equal("board", ex.ParamName);
    }

    [Fact]
    public void Constructor_PlacedValue_RemovesThatCandidateFromPeersOnly()
    {
        var board = new Board();
        board.Set(0, 0, 1);

        var grid = new GradingGrid(board);

        Assert.Equal(GradingGrid.AllMask & ~GradingGrid.Bit(1), grid.Candidates(Idx(0, 1)));
        Assert.Equal(GradingGrid.AllMask & ~GradingGrid.Bit(1), grid.Candidates(Idx(8, 0)));
        Assert.Equal(GradingGrid.AllMask & ~GradingGrid.Bit(1), grid.Candidates(Idx(2, 2)));
        Assert.Equal(GradingGrid.AllMask, grid.Candidates(Idx(8, 8)));
    }

    [Fact]
    public void Place_Digit_ClearsTheCellAndSweepsRowColumnAndBoxPeers()
    {
        var grid = new GradingGrid(new Board());

        grid.Place(Idx(0, 0), 5);

        Assert.Equal(5, grid.Value(Idx(0, 0)));
        Assert.Equal(0, grid.Candidates(Idx(0, 0)));
        Assert.False(grid.HasCandidate(Idx(0, 8), 5));
        Assert.False(grid.HasCandidate(Idx(8, 0), 5));
        Assert.False(grid.HasCandidate(Idx(2, 2), 5));
        Assert.True(grid.HasCandidate(Idx(8, 8), 5));
    }

    [Fact]
    public void EliminateMask_CandidatePresent_RemovesItAndReportsChange()
    {
        var grid = new GradingGrid(new Board());

        var changed = grid.EliminateMask(Idx(0, 0), GradingGrid.Bit(3));

        Assert.True(changed);
        Assert.False(grid.HasCandidate(Idx(0, 0), 3));
    }

    [Fact]
    public void EliminateMask_CandidateAlreadyAbsent_ReportsNoChange()
    {
        var grid = new GradingGrid(new Board());
        grid.EliminateMask(Idx(0, 0), GradingGrid.Bit(3));

        var changedAgain = grid.EliminateMask(Idx(0, 0), GradingGrid.Bit(3));

        Assert.False(changedAgain);
    }

    [Fact]
    public void Evaluate_EmptyBoard_ReportsInProgress()
    {
        var grid = new GradingGrid(new Board());

        Assert.Equal(GridState.InProgress, grid.Evaluate());
    }

    [Fact]
    public void Evaluate_CompletedBoard_ReportsSolved()
    {
        var grid = new GradingGrid(TestBoards.CanonicalSolvedBoard());

        Assert.Equal(GridState.Solved, grid.Evaluate());
    }

    [Fact]
    public void Evaluate_EmptyCellWithNoCandidates_ReportsBroken()
    {
        var grid = new GradingGrid(BoardWithADeadCell());

        Assert.Equal(GridState.Broken, grid.Evaluate());
    }

    // (0,0) is empty but its row supplies 1..8 and its column supplies 9,
    // leaving zero candidates: logic can never finish this position.
    internal static Board BoardWithADeadCell()
    {
        var board = new Board();
        for (int c = 1; c <= 8; c++) board.Set(0, c, c);
        board.Set(5, 0, 9);
        return board;
    }

    internal static int Idx(int row, int col) => row * 9 + col;
}

public class SinglesTechniqueTests
{
    [Fact]
    public void Apply_NakedSingle_PlacesTheOnlyRemainingCandidate()
    {
        // Row 0 holds 1..8, so (0,8) has exactly one candidate left: 9.
        var board = new Board();
        board.Set(0, 0, 1);
        board.Set(0, 1, 2);
        board.Set(0, 2, 3);
        board.Set(0, 3, 4);
        board.Set(0, 4, 5);
        board.Set(0, 5, 6);
        board.Set(0, 6, 7);
        board.Set(0, 7, 8);
        var grid = new GradingGrid(board);

        var progressed = new SinglesTechnique().Apply(grid);

        Assert.True(progressed);
        Assert.Equal(9, grid.Value(GradingGridTests.Idx(0, 8)));
    }

    [Fact]
    public void Apply_HiddenSingle_PlacesTheDigitWithExactlyOneHome()
    {
        // Eight 1s in mutually consistent cells block digit 1 from every row-0
        // column except column 0, while (0,0) itself keeps all nine candidates -
        // so this is provably not a naked single.
        var board = new Board();
        board.Set(1, 3, 1);
        board.Set(2, 6, 1);
        board.Set(3, 1, 1);
        board.Set(4, 4, 1);
        board.Set(5, 7, 1);
        board.Set(6, 2, 1);
        board.Set(7, 5, 1);
        board.Set(8, 8, 1);
        var grid = new GradingGrid(board);
        Assert.Equal(9, GradingGrid.PopCount(grid.Candidates(GradingGridTests.Idx(0, 0))));

        var progressed = new SinglesTechnique().Apply(grid);

        Assert.True(progressed);
        Assert.Equal(1, grid.Value(GradingGridTests.Idx(0, 0)));
    }

    [Fact]
    public void Apply_NoSingleAvailable_ReturnsFalseAndPlacesNothing()
    {
        var grid = new GradingGrid(new Board());

        var progressed = new SinglesTechnique().Apply(grid);

        Assert.False(progressed);
        Assert.Equal(0, grid.Value(GradingGridTests.Idx(4, 4)));
    }
}

public class LockedCandidatesTechniqueTests
{
    [Fact]
    public void Apply_PointingPair_RemovesTheDigitFromTheRowOutsideTheBox()
    {
        // Box 0's rows 1 and 2 are filled with 2..7, so its 1-candidates sit
        // only in row 0 - meaning 1 can be removed from row 0 outside the box.
        // The blockers are values inside the box, not 1s elsewhere: a 1 in any
        // band neighbour would pre-eliminate the very cells the pointing move
        // is supposed to clear.
        var board = new Board();
        board.Set(1, 0, 2);
        board.Set(1, 1, 3);
        board.Set(1, 2, 4);
        board.Set(2, 0, 5);
        board.Set(2, 1, 6);
        board.Set(2, 2, 7);
        var grid = new GradingGrid(board);
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 7), 1));

        var progressed = new LockedCandidatesTechnique().Apply(grid);

        Assert.True(progressed);
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(0, 3), 1));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(0, 6), 1));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(0, 7), 1));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(0, 8), 1));
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 0), 1));
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 1), 1));
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 2), 1));
    }

    [Fact]
    public void Apply_ClaimingRow_RemovesTheDigitFromTheRestOfTheBox()
    {
        // Row 0 holds 1,2,3,4,6,7 in columns 3..8, so its missing 5 fits only
        // in columns 0..2 - all box 0 - and 5 leaves the rest of box 0.
        var board = new Board();
        board.Set(0, 3, 1);
        board.Set(0, 4, 2);
        board.Set(0, 5, 3);
        board.Set(0, 6, 4);
        board.Set(0, 7, 6);
        board.Set(0, 8, 7);
        var grid = new GradingGrid(board);
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(1, 1), 5));

        var progressed = new LockedCandidatesTechnique().Apply(grid);

        Assert.True(progressed);
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(1, 0), 5));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(1, 1), 5));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(1, 2), 5));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(2, 0), 5));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(2, 1), 5));
        Assert.False(grid.HasCandidate(GradingGridTests.Idx(2, 2), 5));
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 0), 5));
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 1), 5));
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 2), 5));
    }

    [Fact]
    public void Apply_NoDigitConfinedAnywhere_ReturnsFalseAndEliminatesNothing()
    {
        // A single clue confines nothing to one row, column or box.
        var board = new Board();
        board.Set(1, 0, 1);
        var grid = new GradingGrid(board);

        var progressed = new LockedCandidatesTechnique().Apply(grid);

        Assert.False(progressed);
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(0, 3), 1));
        Assert.True(grid.HasCandidate(GradingGridTests.Idx(8, 8), 9));
    }
}

public class NakedPairsTechniqueTests
{
    [Fact]
    public void Apply_NakedPairInRow_StripsThePairDigitsFromTheThirdEmptyCell()
    {
        // Row 0 holds 3..8; the 9 at (2,0) removes 9 from (0,0) and (0,1) via
        // box 0, leaving both with exactly {1,2}. (0,8) still allows {1,2,9}.
        var board = new Board();
        board.Set(0, 2, 3);
        board.Set(0, 3, 4);
        board.Set(0, 4, 5);
        board.Set(0, 5, 6);
        board.Set(0, 6, 7);
        board.Set(0, 7, 8);
        board.Set(2, 0, 9);
        var grid = new GradingGrid(board);

        var progressed = new NakedPairsTechnique().Apply(grid);

        Assert.True(progressed);
        Assert.Equal(GradingGrid.Bit(9), grid.Candidates(GradingGridTests.Idx(0, 8)));
        Assert.Equal(GradingGrid.Bit(1) | GradingGrid.Bit(2), grid.Candidates(GradingGridTests.Idx(0, 0)));
        Assert.Equal(GradingGrid.Bit(1) | GradingGrid.Bit(2), grid.Candidates(GradingGridTests.Idx(0, 1)));
    }

    [Fact]
    public void Apply_ThreeCellsSharingThreeCandidates_FindsNoPair()
    {
        // Without the 9-blocker all three empties in row 0 hold {1,2,9}: three
        // candidates each, so no naked pair exists anywhere.
        var board = new Board();
        board.Set(0, 2, 3);
        board.Set(0, 3, 4);
        board.Set(0, 4, 5);
        board.Set(0, 5, 6);
        board.Set(0, 6, 7);
        board.Set(0, 7, 8);
        var grid = new GradingGrid(board);

        var progressed = new NakedPairsTechnique().Apply(grid);

        Assert.False(progressed);
        Assert.Equal(
            GradingGrid.Bit(1) | GradingGrid.Bit(2) | GradingGrid.Bit(9),
            grid.Candidates(GradingGridTests.Idx(0, 8)));
    }

    [Fact]
    public void Apply_EmptyGrid_ReturnsFalse()
    {
        var grid = new GradingGrid(new Board());

        var progressed = new NakedPairsTechnique().Apply(grid);

        Assert.False(progressed);
    }
}

public class PuzzleGraderEdgeTests
{
    [Fact]
    public void Constructor_EmptyTechniqueList_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PuzzleGrader(Array.Empty<IGradingTechnique>()));

        Assert.Equal("techniques", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullTechniques_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PuzzleGrader(null!));

        Assert.Equal("techniques", ex.ParamName);
    }

    [Fact]
    public void Grade_NullBoard_ThrowsArgumentNullException()
    {
        var grader = new PuzzleGrader();

        var ex = Assert.Throws<ArgumentNullException>(() => grader.Grade(null!));

        Assert.Equal("board", ex.ParamName);
    }

    [Fact]
    public void Grade_ContradictoryPosition_ReturnsAdvanced()
    {
        var grader = new PuzzleGrader();

        var tier = grader.Grade(GradingGridTests.BoardWithADeadCell());

        Assert.Equal(TechniqueTier.Advanced, tier);
    }

    [Fact]
    public void Grade_FixturePuzzleSolvableBySinglesAlone_ReturnsSingles()
    {
        var grader = new PuzzleGrader();

        var tier = grader.Grade(TestBoards.Puzzle());

        Assert.Equal(TechniqueTier.Singles, tier);
    }
}
