using System.Globalization;
using MiniDB.Core.DataTypes;

namespace MiniDB.Tests.DataTypes;

public sealed class StandardTypeTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("-42", -42)]
    [InlineData("+42", 42)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void Integer_parses_valid_values(string input, int expected)
    {
        var type = new IntegerType();
        Assert.True(type.Validate(input));
        Assert.Equal(expected, Assert.IsType<int>(type.Parse(input)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("test")]
    public void Integer_rejects_invalid_values(string? input) =>
        Assert.False(new IntegerType().Validate(input));

    [Theory]
    [InlineData("0", 0d)]
    [InlineData("-3.14", -3.14)]
    [InlineData("1.25e2", 125d)]
    public void Real_parses_valid_values(string input, double expected)
    {
        var type = new RealType();
        Assert.True(type.Validate(input));
        Assert.Equal(expected, Assert.IsType<double>(type.Parse(input)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,5")]
    [InlineData("1,000.5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e9999")]
    public void Real_rejects_invalid_or_nonfinite_values(string? input) =>
        Assert.False(new RealType().Validate(input));

    [Fact]
    public void Numeric_parsing_does_not_depend_on_current_culture()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            Assert.Equal(1.5d, new RealType().Parse("1.5"));
            Assert.False(new RealType().Validate("1,5"));
            Assert.Equal(42, new IntegerType().Parse("42"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("a", 'a')]
    [InlineData("ї", 'ї')]
    [InlineData(" ", ' ')]
    public void Char_accepts_one_character(string input, char expected)
    {
        var type = new CharType();
        Assert.True(type.Validate(input));
        Assert.Equal(expected, Assert.IsType<char>(type.Parse(input)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("😀")]
    public void Char_rejects_values_that_do_not_fit_one_CSharp_char(string? input) =>
        Assert.False(new CharType().Validate(input));

    [Theory]
    [InlineData("")]
    [InlineData("  text  ")]
    [InlineData("будь-який текст\n😀")]
    public void String_preserves_input_including_empty_strings(string input)
    {
        var type = new StringType();
        Assert.True(type.Validate(input));
        Assert.Equal(input, Assert.IsType<string>(type.Parse(input)));
    }

    [Fact]
    public void All_types_have_expected_names_and_reject_null()
    {
        IDataType[] types = [new IntegerType(), new RealType(), new CharType(),
            new StringType(), new ColorType(), new ColorIntervalType()];

        Assert.Equal(new[] { "integer", "real", "char", "string", "color", "colorInvl" },
            types.Select(type => type.Name));

        foreach (var type in types)
        {
            Assert.False(type.Validate(null));
            Assert.Throws<ArgumentNullException>(() => type.Parse(null!));
        }
    }

    [Fact]
    public void Invalid_nonnull_text_throws_FormatException()
    {
        IDataType[] types = [new IntegerType(), new RealType(), new CharType(),
            new ColorType(), new ColorIntervalType()];

        foreach (var type in types)
        {
            Assert.False(type.Validate("invalid"));
            Assert.Throws<FormatException>(() => type.Parse("invalid"));
        }
    }
}
