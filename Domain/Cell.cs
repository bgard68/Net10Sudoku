namespace Sudoku.Domain;

// A cell is readable by anyone but mutable only through Board (the mutators are
// internal), so every write goes through the domain's own invariant checks and
// outside layers cannot bypass the rules.
public sealed class Cell
{
    private readonly HashSet<int> _notes = new();

    public int Row { get; }
    public int Col { get; }
    public int? Value { get; private set; }
    public bool IsGiven { get; private set; }

    // Pencil-mark candidates the player has jotted into an empty cell.
    public IReadOnlyCollection<int> Notes => _notes;

    internal Cell(int row, int col)
    {
        Row = row;
        Col = col;
    }

    public bool HasNote(int value) => _notes.Contains(value);

    internal void Set(int? value, bool given = false)
    {
        // Prevent modifying cells that are marked as given (puzzle clues).
        if (IsGiven && !given)
            throw new InvalidOperationException("Cannot modify a given cell.");

        Value = value;
        // A real value supersedes any pencil marks.
        if (value is not null) _notes.Clear();
        if (given) IsGiven = true;
    }

    internal void ToggleNote(int value)
    {
        // Notes only make sense on an empty, editable cell.
        if (IsGiven || Value is not null) return;
        if (!_notes.Remove(value)) _notes.Add(value);
    }

    internal void RemoveNote(int value) => _notes.Remove(value);

    internal void ClearNotes() => _notes.Clear();
}
