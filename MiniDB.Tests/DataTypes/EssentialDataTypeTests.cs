using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

namespace MiniDB.Tests.DataTypes;

public sealed class EssentialDataTypeTests
{
    [Fact]
    public void Integer_AcceptsWholeNumbersAndRejectsOtherText()
    {
        var type = new IntegerType();
        Assert.True(type.Validate("42"));
        Assert.Equal(42, type.Parse("42"));
        Assert.False(type.Validate("1.25"));
        Assert.Throws<FormatException>(() => type.Parse("hello"));
    }

    [Fact]
    public void Color_ParsesRgbAndRejectsOutOfRangeComponents()
    {
        var type = new ColorType();
        Assert.True(type.Validate("RGB(255, 0, 0)"));
        Assert.Equal(new Color(255, 0, 0), type.Parse("RGB(255, 0, 0)"));
        Assert.False(type.Validate("RGB(256, 0, 0)"));
        Assert.Throws<FormatException>(() => type.Parse("RGB(-1, 0, 0)"));
    }

    [Fact]
    public void ColorInterval_RequiresOrderedRgbBounds()
    {
        var type = new ColorIntervalType();
        const string valid = "[RGB(10, 20, 30); RGB(100, 120, 150)]";
        Assert.True(type.Validate(valid));
        Assert.Equal(new ColorInterval(new Color(10, 20, 30), new Color(100, 120, 150)), type.Parse(valid));
        const string invalid = "[RGB(100, 20, 30); RGB(10, 120, 150)]";
        Assert.False(type.Validate(invalid));
        Assert.Throws<FormatException>(() => type.Parse(invalid));
    }
}
