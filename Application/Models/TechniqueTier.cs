namespace Sudoku.Application.Models;

// The hardest kind of logical deduction a puzzle demands of the player.
// Ordering matters: higher values are harder, and the grader reports the
// maximum tier it had to use to finish the puzzle.
public enum TechniqueTier
{
    Singles = 0,         // naked and hidden singles are enough
    LockedCandidate = 1, // needs pointing/claiming eliminations
    Pair = 2,            // needs naked pairs
    Advanced = 3         // beyond the implemented techniques (fish, chains, guessing)
}
