using Sudoku.Application.Interfaces;
using Sudoku.Application.Models;
using Sudoku.Application.Services;
using Sudoku.Domain;
using Sudoku.Infrastructure;

namespace Sudoku.Tests;

// True unit tests for the coordinator: the random generator and backtracking
// solver are replaced with deterministic stubs at their interface boundary, so
// every test runs on the fixed TestBoards puzzle in microseconds. The validator
// and conflict detector stay real - they are pure functions, and stubbing them
// would only let the tests drift from shipped behaviour.
public class SudokuServiceUnitTests
{
    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullExceptionNamingTheParameter()
    {
        var validator = new SudokuValidator();
        var solver = new ScriptedSolver(solvable: true);
        var generator = new StubGenerator(withSolution: true);
        var conflicts = new ConflictDetector(validator);

        var noGenerator = Assert.Throws<ArgumentNullException>(() => new SudokuService(null!, solver, validator, conflicts));
        var noSolver = Assert.Throws<ArgumentNullException>(() => new SudokuService(generator, null!, validator, conflicts));
        var noValidator = Assert.Throws<ArgumentNullException>(() => new SudokuService(generator, solver, null!, conflicts));
        var noConflicts = Assert.Throws<ArgumentNullException>(() => new SudokuService(generator, solver, validator, null!));

        Assert.Equal("generator", noGenerator.ParamName);
        Assert.Equal("solver", noSolver.ParamName);
        Assert.Equal("validator", noValidator.ParamName);
        Assert.Equal("conflicts", noConflicts.ParamName);
    }

    [Fact]
    public void Place_WithoutSelection_ChangesNothingAndRecordsNoHistory()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);

        svc.Place(5);

        Assert.Null(svc.Current.Get(0, 0));
        Assert.False(svc.CanUndo);
        Assert.Equal(0, svc.Mistakes);
    }

    [Fact]
    public void Place_OnGivenCell_KeepsTheClueAndRecordsNoHistory()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(2, 2); // given clue, canonical value 9

        svc.Place(1);

        Assert.Equal(9, svc.Current.Get(2, 2));
        Assert.False(svc.CanUndo);
        Assert.Equal(0, svc.Mistakes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(-3)]
    public void Place_DigitOutsideOneToNine_IsIgnoredEntirely(int value)
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);

        svc.Place(value);

        Assert.Null(svc.Current.Get(0, 0));
        Assert.False(svc.CanUndo);
        Assert.Equal(0, svc.Mistakes);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 9)]
    public void Select_OffGridCoordinates_KeepsThePreviousSelection(int row, int col)
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(4, 4);

        svc.Select(row, col);

        Assert.Equal(new Position(4, 4), svc.Selected);
    }

    [Fact]
    public void Place_CorrectValue_FillsTheCellWithoutCountingAMistake()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(0, 0); // solution value is 1

        svc.Place(1);

        Assert.Equal(1, svc.Current.Get(0, 0));
        Assert.Equal(0, svc.Mistakes);
        Assert.True(svc.CanUndo);
    }

    [Fact]
    public void Place_WrongValue_CountsExactlyOneMistakeAndStillWritesTheValue()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(0, 0); // solution value is 1

        svc.Place(2);

        Assert.Equal(2, svc.Current.Get(0, 0));
        Assert.Equal(1, svc.Mistakes);
    }

    [Fact]
    public void Place_SameValueOnSameCellTwice_IsANoOpTheSecondTime()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);
        svc.Place(2); // wrong once: one mistake, one history entry

        svc.Place(2);

        Assert.Equal(1, svc.Mistakes);
        svc.Undo();
        Assert.Null(svc.Current.Get(0, 0));
        Assert.False(svc.CanUndo); // a second Place recorded nothing
    }

    [Fact]
    public void Place_WithoutRecordedSolution_NeverCountsMistakes()
    {
        var svc = Service(withSolution: false);
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);

        svc.Place(7); // wrong for this cell, but no answer key exists

        Assert.Equal(7, svc.Current.Get(0, 0));
        Assert.Equal(0, svc.Mistakes);
    }

    [Fact]
    public void Place_InNotesMode_TogglesANoteInsteadOfWritingAValue()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);
        svc.ToggleNotesMode();

        svc.Place(3);

        Assert.Null(svc.Current.Get(0, 0));
        Assert.True(svc.Current[0, 0].HasNote(3));
        Assert.Equal(0, svc.Mistakes);
        Assert.True(svc.CanUndo);
    }

    [Fact]
    public void Place_InNotesModeOnFilledCell_IsIgnored()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);
        svc.Place(1);
        svc.ToggleNotesMode();

        svc.Place(4);

        Assert.Equal(1, svc.Current.Get(0, 0));
        Assert.Empty(svc.Current[0, 0].Notes);
        svc.Undo();
        Assert.False(svc.CanUndo); // only the real placement was recorded
    }

    [Fact]
    public void Place_Value_SweepsThatNoteFromRowColumnAndBoxPeersOnly()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.ToggleNotesMode();
        svc.Select(1, 0); // row peer of (1,1) and box peer
        svc.Place(5);
        svc.Select(0, 1); // column peer of (1,1) and box peer
        svc.Place(5);
        svc.Select(0, 0); // box peer of (1,1)
        svc.Place(5);
        svc.Select(4, 4); // unrelated cell: different row, column and box
        svc.Place(5);
        svc.ToggleNotesMode();
        svc.Select(1, 1); // solution value is 5

        svc.Place(5);

        Assert.Equal(5, svc.Current.Get(1, 1));
        Assert.False(svc.Current[1, 0].HasNote(5));
        Assert.False(svc.Current[0, 1].HasNote(5));
        Assert.False(svc.Current[0, 0].HasNote(5));
        Assert.True(svc.Current[4, 4].HasNote(5));
    }

    [Fact]
    public void Clear_EmptyCellWithoutNotes_RecordsNoHistory()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(4, 4);

        svc.Clear();

        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Clear_CellHoldingOnlyNotes_RemovesThemInOneUndoableStep()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.ToggleNotesMode();
        svc.Select(4, 4);
        svc.Place(3);
        svc.ToggleNotesMode();

        svc.Clear();

        Assert.Empty(svc.Current[4, 4].Notes);
        svc.Undo();
        Assert.True(svc.Current[4, 4].HasNote(3));
    }

    [Fact]
    public void ClearAll_BoardWithoutPlayerMarks_RecordsNoHistory()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);

        svc.ClearAll();

        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Restore_NegativeMistakeCount_IsClampedToZero()
    {
        var svc = Service();

        svc.Restore(TestBoards.Puzzle(), mistakes: -5);

        Assert.Equal(0, svc.Mistakes);
    }

    [Fact]
    public void Restore_NullBoard_ThrowsArgumentNullException()
    {
        var svc = Service();

        var ex = Assert.Throws<ArgumentNullException>(() => svc.Restore(null!));

        Assert.Equal("board", ex.ParamName);
    }

    [Fact]
    public async Task NewAsync_AfterADirtyGame_ResetsSelectionMistakesAndHistory()
    {
        var svc = Service();
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);
        svc.Place(2); // mistake + history + selection all dirty

        await svc.NewAsync(Difficulty.Easy);

        Assert.Null(svc.Selected);
        Assert.Equal(0, svc.Mistakes);
        Assert.False(svc.CanUndo);
        Assert.Null(svc.Current.Get(0, 0));
    }

    [Fact]
    public void Solve_WithoutRecordedSolution_FillsEveryCellFromTheSolver()
    {
        var svc = Service(withSolution: false);
        svc.New(Difficulty.Easy);

        var solved = svc.Solve();

        Assert.True(solved);
        Assert.Equal(1, svc.Current.Get(0, 0));
        Assert.Equal(9, svc.Current.Get(4, 4));
        Assert.True(svc.IsComplete());
        Assert.True(svc.CanUndo);
    }

    [Fact]
    public void Solve_SolverCannotFinishTheGivens_ReturnsFalseAndTouchesNothing()
    {
        var svc = Service(withSolution: false, solvable: false);
        svc.New(Difficulty.Easy);

        var solved = svc.Solve();

        Assert.False(solved);
        Assert.Null(svc.Current.Get(0, 0));
        Assert.False(svc.CanUndo); // a failed solve must not burn an undo step
    }

    [Fact]
    public void GetHintForSelectedCell_WithoutRecordedSolution_FallsBackToSolvingTheGivens()
    {
        var svc = Service(withSolution: false);
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);

        var hint = svc.GetHintForSelectedCell();

        Assert.Equal((new Position(0, 0), 1), hint);
    }

    [Fact]
    public void GetHintForSelectedCell_WithoutSolutionAndUnsolvableGivens_ReturnsNull()
    {
        var svc = Service(withSolution: false, solvable: false);
        svc.New(Difficulty.Easy);
        svc.Select(0, 0);

        var hint = svc.GetHintForSelectedCell();

        Assert.Null(hint);
    }

    // ----- fixture graph -------------------------------------------------

    private static SudokuService Service(bool withSolution = true, bool solvable = true)
    {
        var validator = new SudokuValidator();
        return new SudokuService(
            new StubGenerator(withSolution),
            new ScriptedSolver(solvable),
            validator,
            new ConflictDetector(validator));
    }

    // Hands out the fixed TestBoards puzzle instead of carving a random one.
    private sealed class StubGenerator : ISudokuGenerator
    {
        private readonly bool _withSolution;

        public StubGenerator(bool withSolution) => _withSolution = withSolution;

        public Board Generate(Difficulty difficulty) => TestBoards.Puzzle(_withSolution);
    }

    // Fills empties from the canonical grid, or reports failure when scripted
    // to - the two behaviours the coordinator must handle from a real solver.
    private sealed class ScriptedSolver : ISudokuSolver
    {
        private readonly bool _solvable;

        public ScriptedSolver(bool solvable) => _solvable = solvable;

        public bool TrySolve(Board board)
        {
            if (!_solvable) return false;
            for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                if (board.Get(r, c) is null) board.Set(r, c, TestBoards.CanonicalValueAt(r, c));
            }
            return true;
        }

        public int CountSolutions(Board board, int limit) => _solvable ? Math.Min(1, limit) : 0;
    }
}
