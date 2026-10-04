using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

namespace MiniDB.Tests.DataTypes;

public sealed class ColorTypeTests
{
    [Theory]
    [InlineData("RGB(255, 0, 0)", 255, 0, 0)]
    [InlineData("RGB(0, 0, 0)", 0, 0, 0)]
    [InlineData("RGB(120, 45, 200)", 120, 45, 200)]
    [InlineData("RGB(255,255,255)", 255, 255, 255)]
    [InlineData("  RGB( 10 , 20 , 30 )  ", 10, 20, 30)]
    public void Parses_valid_colors(string input, int red, int green, int blue)
    {
        var type = new ColorType();
        Assert.True(type.Validate(input));
        var color = Assert.IsType<Color>(type.Parse(input));
        Assert.Equal(new Color(red, green, blue), color);
        Assert.Equal(color, type.Parse(color.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("RGB(256, 0, 0)")]
    [InlineData("RGB(0, 256, 0)")]
    [InlineData("RGB(0, 0, 256)")]
    [InlineData("RGB(-1, 0, 0)")]
    [InlineData("RGB(0, -1, 0)")]
    [InlineData("RGB(0, 0, -1)")]
    [InlineData("255,0,0")]
    [InlineData("RGB(test, 0, 0)")]
    [InlineData("RGB(99999999999999999999999999, 0, 0)")]
    [InlineData("RGB(1, 2)")]
    [InlineData("RGB(1, 2, 3, 4)")]
    [InlineData("RGB(1.5, 2, 3)")]
    [InlineData("before RGB(1, 2, 3)")]
    [InlineData("RGB(1, 2, 3) after")]
    public void Rejects_invalid_colors(string? input)
    {
        var type = new ColorType();
        Assert.False(type.Validate(input));

        if (input is not null)
        {
            Assert.Throws<FormatException>(() => type.Parse(input));
        }
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(256, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 256, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 256)]
    public void Model_constructor_enforces_all_RGB_ranges(int red, int green, int blue) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Color(red, green, blue));
}
