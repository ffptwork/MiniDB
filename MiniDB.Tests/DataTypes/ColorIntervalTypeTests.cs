using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

namespace MiniDB.Tests.DataTypes;

public sealed class ColorIntervalTypeTests
{
    [Fact]
    public void Parses_requested_interval_and_preserves_bounds()
    {
        var type = new ColorIntervalType();
        const string input = "[RGB(10, 20, 30); RGB(100, 120, 150)]";
        Assert.True(type.Validate(input));
        var interval = Assert.IsType<ColorInterval>(type.Parse(input));
        Assert.Equal(new Color(10, 20, 30), interval.MinColor);
        Assert.Equal(new Color(100, 120, 150), interval.MaxColor);
        Assert.Equal(interval, type.Parse(interval.ToString()));
    }

    [Theory]
    [InlineData("[RGB(0, 0, 0); RGB(255, 255, 255)]")]
    [InlineData("[RGB(10, 20, 30); RGB(10, 20, 30)]")]
    [InlineData(" [ RGB(0,0,0) ; RGB(1,2,3) ] ")]
    public void Accepts_valid_boundaries_and_whitespace(string input) =>
        Assert.True(new ColorIntervalType().Validate(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[RGB(11, 20, 30); RGB(10, 120, 150)]")]
    [InlineData("[RGB(10, 121, 30); RGB(100, 120, 150)]")]
    [InlineData("[RGB(10, 20, 151); RGB(100, 120, 150)]")]
    [InlineData("[RGB(256, 20, 30); RGB(255, 120, 150)]")]
    [InlineData("[RGB(0, 0, 0); RGB(100, 120, 256)]")]
    [InlineData("[RGB(-1, 0, 0); RGB(100, 120, 150)]")]
    [InlineData("RGB(10, 20, 30); RGB(100, 120, 150)")]
    [InlineData("[RGB(10, 20, 30), RGB(100, 120, 150)]")]
    [InlineData("[RGB(10, 20, 30); RGB(100, 120, 150); RGB(255, 255, 255)]")]
    [InlineData("[RGB(test, 0, 0); RGB(100, 120, 150)]")]
    [InlineData("[RGB(10, 20, 30); RGB(100, 120, 150)] trailing")]
    public void Rejects_invalid_intervals(string? input)
    {
        var type = new ColorIntervalType();
        Assert.False(type.Validate(input));

        if (input is not null)
        {
            Assert.Throws<FormatException>(() => type.Parse(input));
        }
    }

    [Theory]
    [InlineData(11, 20, 30)]
    [InlineData(10, 121, 30)]
    [InlineData(10, 20, 151)]
    public void Model_constructor_rejects_reversed_component(int red, int green, int blue) =>
        Assert.Throws<ArgumentException>(() =>
            new ColorInterval(new Color(red, green, blue), new Color(10, 120, 150)));

    [Fact]
    public void Model_constructor_rejects_null_bounds()
    {
        var color = new Color(0, 0, 0);
        Assert.Throws<ArgumentNullException>(() => new ColorInterval(null!, color));
        Assert.Throws<ArgumentNullException>(() => new ColorInterval(color, null!));
    }
}
