using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

namespace MiniDB.Tests.DataTypes;

public sealed class TypedValueTests
{
    [Fact]
    public void Each_type_accepts_its_stored_value_and_rejects_null()
    {
        (IDataType Type, string Input)[] examples =
        [
            (new IntegerType(), "42"),
            (new RealType(), "1.5"),
            (new CharType(), "ї"),
            (new StringType(), ""),
            (new ColorType(), "RGB(10, 20, 30)"),
            (new ColorIntervalType(), "[RGB(0, 0, 0); RGB(255, 255, 255)]")
        ];

        foreach (var (type, input) in examples)
        {
            Assert.True(type.ValidateValue(type.Parse(input)));
            Assert.False(type.ValidateValue(null));
        }
    }

    [Fact]
    public void Does_not_convert_values_of_the_wrong_runtime_type()
    {
        Assert.False(new IntegerType().ValidateValue("42"));
        Assert.False(new IntegerType().ValidateValue(42L));
        Assert.False(new RealType().ValidateValue(1));
        Assert.False(new RealType().ValidateValue(1.5f));
        Assert.False(new CharType().ValidateValue("a"));
        Assert.False(new StringType().ValidateValue('a'));
        Assert.False(new ColorType().ValidateValue("RGB(0, 0, 0)"));
        Assert.False(new ColorIntervalType().ValidateValue(new Color(0, 0, 0)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Real_rejects_nonfinite_stored_values(double value) =>
        Assert.False(new RealType().ValidateValue(value));
}
