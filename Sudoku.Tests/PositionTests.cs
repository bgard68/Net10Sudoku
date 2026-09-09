using Sudoku.Domain;

namespace Sudoku.Tests;

public class PositionTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 8)]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    [InlineData(4, 4)]
    public void IsValid_OnGridCoordinates_ReturnsTrue(int row, int col)
    {
        var position = new Position(row, col);

        Assert.True(position.IsValid);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(9, 0)]
    [InlineData(0, 9)]
    [InlineData(int.MinValue, 4)]
    [InlineData(4, int.MaxValue)]
    public void IsValid_OffGridCoordinates_ReturnsFalse(int row, int col)
    {
        var position = new Position(row, col);

        Assert.False(position.IsValid);
    }

    [Fact]
    public void Equals_SameCoordinates_AreEqualWithSameHashCode()
    {
        var a = new Position(3, 4);
        var b = new Position(3, 4);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_TransposedCoordinates_AreNotEqual()
    {
        var a = new Position(3, 4);
        var b = new Position(4, 3);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Deconstruct_ReturnsRowThenCol()
    {
        var position = new Position(2, 7);

        var (row, col) = position;

        Assert.Equal(2, row);
        Assert.Equal(7, col);
    }
}
