using MiniDB.Core.DataTypes;

namespace MiniDB.Tests.DataTypes;

public sealed class DataTypeFactoryTests
{
    [Theory]
    [InlineData("integer", typeof(IntegerType))]
    [InlineData("real", typeof(RealType))]
    [InlineData("char", typeof(CharType))]
    [InlineData("string", typeof(StringType))]
    [InlineData("color", typeof(ColorType))]
    [InlineData("colorInvl", typeof(ColorIntervalType))]
    public void Restores_stable_names(string name, Type expectedType)
    {
        var type = DataTypeFactory.Create(name);
        Assert.IsType(expectedType, type);
        Assert.Equal(name, type.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    [InlineData("Integer")]
    [InlineData("colorinvl")]
    public void Rejects_missing_unknown_and_wrong_case_names(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => DataTypeFactory.Create(name!));
}
