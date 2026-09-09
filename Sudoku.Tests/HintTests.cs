using Sudoku.Application.Models;
using Sudoku.Application.Services;
using Sudoku.Domain;

namespace Sudoku.Tests;

public class HintTests
{
    // Regression: hints used to be derived by solving the live board, so a single
    // incorrect entry anywhere made the puzzle unsolvable and every hint returned null.
    [Fact]
    public void GetHintForSelectedCell_WrongValueInADifferentCell_StillAnswersEveryOtherCell()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (wrongRow, wrongCol) = TestGame.FirstEmptyCell(svc.Current);
        svc.Select(wrongRow, wrongCol);
        svc.Place(TestGame.WrongValueFor(svc.Current, wrongRow, wrongCol));

        var (examined, wrongOrMissing) = HintsForEveryEmptyCellExcept(svc, wrongRow, wrongCol);

        Assert.Empty(wrongOrMissing);
        Assert.NotEqual(0, examined);
    }

    // The old implementation skipped cells that already had a value, so asking for a
    // hint on a wrong entry just echoed that same wrong value back.
    [Fact]
    public void GetHintForSelectedCell_SelectedCellHoldsAWrongValue_ReturnsTheCorrection()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var correct = svc.Current.SolutionAt(row, col)!.Value;
        var wrong = TestGame.WrongValueFor(svc.Current, row, col);
        svc.Select(row, col);
        svc.Place(wrong);

        var hint = svc.GetHintForSelectedCell();

        Assert.Equal((new Position(row, col), correct), hint);
        Assert.NotEqual(wrong, hint!.Value.value);
    }

    [Fact]
    public void ApplyHintForSelectedCell_CellHoldsAWrongValue_OverwritesItWithTheCorrectOne()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var correct = svc.Current.SolutionAt(row, col)!.Value;
        svc.Select(row, col);
        svc.Place(TestGame.WrongValueFor(svc.Current, row, col));

        svc.ApplyHintForSelectedCell();

        Assert.Equal(correct, svc.Current.Get(row, col));
    }

    [Fact]
    public void GetHintForSelectedCell_GivenCellSelected_ReturnsNull()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = FirstGivenCell(svc.Current);
        svc.Select(row, col);

        Assert.Null(svc.GetHintForSelectedCell());
    }

    [Fact]
    public void ApplyHintForSelectedCell_GivenCellSelected_LeavesTheClueUntouched()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        var (row, col) = FirstGivenCell(svc.Current);
        var clue = svc.Current.Get(row, col);
        svc.Select(row, col);

        svc.ApplyHintForSelectedCell();

        Assert.Equal(clue, svc.Current.Get(row, col));
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void GetHintForSelectedCell_NothingSelected_ReturnsNull()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        svc.ClearSelection();

        Assert.Null(svc.GetHintForSelectedCell());
    }

    [Fact]
    public void ApplyHintForSelectedCell_NothingSelected_RecordsNoHistory()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Easy);
        svc.ClearSelection();

        svc.ApplyHintForSelectedCell();

        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void GetHintForSelectedCell_UntouchedBoard_MatchesTheRecordedSolution()
    {
        var svc = TestGame.Service();
        svc.New(Difficulty.Medium);
        var (row, col) = TestGame.FirstEmptyCell(svc.Current);
        var expected = svc.Current.SolutionAt(row, col)!.Value;
        svc.Select(row, col);

        var hint = svc.GetHintForSelectedCell();

        Assert.Equal((new Position(row, col), expected), hint);
    }

    // Walks every editable empty cell except the one named, returning how many
    // were examined and a description of each that failed to yield the correct
    // hint. Scanning lives here so the test body stays branch-free.
    private static (int Examined, List<string> WrongOrMissing) HintsForEveryEmptyCellExcept(
        SudokuService svc, int skipRow, int skipCol)
    {
        var failures = new List<string>();
        int examined = 0;

        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
        {
            if (svc.Current[r, c].IsGiven) continue;
            if (r == skipRow && c == skipCol) continue;
            if (svc.Current.Get(r, c) is not null) continue;

            svc.Select(r, c);
            var hint = svc.GetHintForSelectedCell();
            var expected = svc.Current.SolutionAt(r, c);
            examined++;

            if (hint is null) failures.Add($"({r},{c}): no hint offered");
            else if (hint.Value.value != expected)
                failures.Add($"({r},{c}): hinted {hint.Value.value}, expected {expected}");
        }

        return (examined, failures);
    }

    private static (int Row, int Col) FirstGivenCell(Board board)
    {
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            if (board[r, c].IsGiven) return (r, c);

        throw new InvalidOperationException("Board has no given cells.");
    }
}
