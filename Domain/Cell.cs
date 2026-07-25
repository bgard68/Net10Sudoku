namespace Sudoku.Domain;

public sealed class Cell
{
    private readonly HashSet<int> _notes = new();

    public int Row { get; }
    public int Col { get; }
    public int? Value { get; private set; }
    public bool IsGiven { get; private set; }

    // Pencil-mark candidates the player has jotted into an empty cell.
    public IReadOnlyCollection<int> Notes => _notes;

    public Cell(int row, int col, int? value = null, bool given = false)
    {
        Row = row;
        Col = col;
        Value = value;
        IsGiven = given;
    }

    public void Set(int? value, bool given = false)
    {
        // Prevent modifying cells that are marked as given (puzzle clues).
        if (IsGiven && !given)
            throw new InvalidOperationException("Cannot modify a given cell.");

        Value = value;
        // A real value supersedes any pencil marks.
        if (value is not null) _notes.Clear();
        if (given) IsGiven = true;
    }

    public bool HasNote(int value) => _notes.Contains(value);

    public void ToggleNote(int value)
    {
        // Notes only make sense on an empty, editable cell.
        if (IsGiven || Value is not null) return;
        if (!_notes.Remove(value)) _notes.Add(value);
    }

    public void RemoveNote(int value) => _notes.Remove(value);

    public void ClearNotes() => _notes.Clear();
}
