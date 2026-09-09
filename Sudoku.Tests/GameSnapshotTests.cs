using Sudoku.Application.Models;

namespace Sudoku.Tests;

public class GameSnapshotTests
{
    [Fact]
    public void Capture_ThenToBoard_RoundTripsValuesGivensNotesAndSolution()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);
        svc.ToggleNotesMode();
        var (row2, col2) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row2, col2);
        svc.Place(3);
        svc.Place(8);

        var snapshot = GameSnapshot.Capture(svc.Current, TimeSpan.FromSeconds(90), Difficulty.Medium);
        var restored = snapshot.ToBoard();

        Assert.Equal(TestBoards.Values(svc.Current), TestBoards.Values(restored));
        Assert.Equal(TestBoards.Givens(svc.Current), TestBoards.Givens(restored));
        Assert.Equal(TestBoards.Notes(svc.Current), TestBoards.Notes(restored));
        Assert.Equal(TestBoards.Solution(svc.Current), TestBoards.Solution(restored));
        Assert.Equal(90, snapshot.ElapsedSeconds);
        Assert.Equal(Difficulty.Medium, snapshot.Difficulty);
    }

    [Fact]
    public void Capture_BoardWithoutARecordedSolution_OmitsTheSolution()
    {
        var snapshot = GameSnapshot.Capture(TestBoards.Puzzle(withSolution: false), TimeSpan.Zero, Difficulty.Easy);

        Assert.Null(snapshot.Solution);
        Assert.False(snapshot.ToBoard().HasSolution);
    }

    [Fact]
    public void ToBoard_RestoredBoard_StillAnswersHintsFromTheRecordedSolution()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var snapshot = GameSnapshot.Capture(svc.Current, TimeSpan.Zero, Difficulty.Easy);

        svc.Restore(snapshot.ToBoard());
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var expected = svc.Current.SolutionAt(row, col)!.Value;
        svc.Select(row, col);
        var hint = svc.GetHintForSelectedCell();

        Assert.Equal((new Domain.Position(row, col), expected), hint);
    }

    [Fact]
    public void Restore_FromASnapshot_ClearsBothUndoAndRedoTrails()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(row, col);
        svc.Place(5);
        Assert.True(svc.CanUndo);

        var snapshot = GameSnapshot.Capture(svc.Current, TimeSpan.Zero, Difficulty.Easy);
        svc.Restore(snapshot.ToBoard());

        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void ToBoard_WronglySizedArrays_ThrowsArgumentException()
    {
        var snapshot = new GameSnapshot
        {
            Values = new int[80],
            Givens = new bool[81],
            NoteMasks = new int[81]
        };

        Assert.Throws<ArgumentException>(() => snapshot.ToBoard());
    }

    [Fact]
    public void ToBoard_WronglySizedSolution_ThrowsArgumentException()
    {
        var snapshot = new GameSnapshot
        {
            Values = new int[81],
            Givens = new bool[81],
            NoteMasks = new int[81],
            Solution = new int[80]
        };

        Assert.Throws<ArgumentException>(() => snapshot.ToBoard());
    }

    [Fact]
    public void ToBoard_ValueOutsideZeroToNine_ThrowsArgumentException()
    {
        var values = new int[81];
        values[0] = 12;

        var snapshot = new GameSnapshot
        {
            Values = values,
            Givens = new bool[81],
            NoteMasks = new int[81]
        };

        Assert.Throws<ArgumentException>(() => snapshot.ToBoard());
    }

    // The JSON deserializer can hand back null arrays despite the nullability
    // annotations when the stored payload was edited by hand; ToBoard must
    // classify that as malformed data, not crash with a null dereference.
    [Fact]
    public void ToBoard_NullArraysFromATamperedPayload_ThrowsArgumentException()
    {
        var nullValues = new GameSnapshot { Values = null!, Givens = new bool[81], NoteMasks = new int[81] };
        var nullGivens = new GameSnapshot { Values = new int[81], Givens = null!, NoteMasks = new int[81] };
        var nullNotes = new GameSnapshot { Values = new int[81], Givens = new bool[81], NoteMasks = null! };

        Assert.Throws<ArgumentException>(() => nullValues.ToBoard());
        Assert.Throws<ArgumentException>(() => nullGivens.ToBoard());
        Assert.Throws<ArgumentException>(() => nullNotes.ToBoard());
    }

    [Fact]
    public void ToBoard_SolutionValueOutsideOneToNine_ThrowsArgumentException()
    {
        var solution = new int[81];
        Array.Fill(solution, 5);
        solution[80] = 0; // a recorded solution must be a complete 1..9 grid

        var snapshot = new GameSnapshot
        {
            Values = new int[81],
            Givens = new bool[81],
            NoteMasks = new int[81],
            Solution = solution
        };

        Assert.Throws<ArgumentException>(() => snapshot.ToBoard());
    }

    [Fact]
    public void Capture_NullBoard_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => GameSnapshot.Capture(null!, TimeSpan.Zero, Difficulty.Easy));

        Assert.Equal("board", ex.ParamName);
    }
}
