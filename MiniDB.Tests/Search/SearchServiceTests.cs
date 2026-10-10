using System.Globalization;
using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Core.Search;

namespace MiniDB.Tests.Search;


public sealed class SearchServiceTests
{
    private readonly SearchService _service = new();

    [Theory]
    [InlineData("Dan", new[] { "Dan" })]
    [InlineData("Dan*", new[] { "Dan", "Daniel", "Danylo" })]
    [InlineData("*test*", new[] { "test", "pretestpost", "before\ntest\nafter" })]
    [InlineData("*", new[] { "Dan", "Daniel", "Danylo", "dan", "test", "pretestpost", "before\ntest\nafter", "", "TEST" })]
    [InlineData("missing*", new string[0])]
    [InlineData("", new[] { "" })]
    [InlineData("**Dan**", new[] { "Dan", "Daniel", "Danylo" })]
    public void String_patterns_follow_case_sensitive_wildcard_rules(string pattern, string[] expected)
    {
        var table = CreateTable(new StringType(),
            "Dan", "Daniel", "Danylo", "dan", "test", "pretestpost", "before\ntest\nafter", "", "TEST");

        var result = _service.Search(table, "Value", pattern);

        Assert.Equal(expected, result.Select(row => (string)row.GetValue("Value")));
    }

    [Theory]
    [InlineData("*Dan", new[] { "Dan", "MrDan" })]
    [InlineData("D*n", new[] { "Dan", "Dn", "Danylon" })]
    [InlineData("D*n*", new[] { "Dan", "Daniel", "Dn", "Danylon" })]
    public void Wildcards_match_zero_or_more_characters_and_the_whole_value(string pattern, string[] expected)
    {
        var table = CreateTable(new StringType(), "Dan", "Daniel", "MrDan", "Dn", "Danylon");
        Assert.Equal(expected,
            _service.Search(table, "Value", pattern).Select(row => (string)row.GetValue("Value")));
    }

    [Fact]
    public void Treats_regex_metacharacters_as_literal_text()
    {
        const string literal = @"a.[b](c)+?^$|\test";
        var table = CreateTable(new StringType(), literal, "axbctest", "prefix" + literal);
        Assert.Same(table.Rows[0], Assert.Single(_service.Search(table, "Value", literal)));
        Assert.Equal(new[] { table.Rows[0], table.Rows[2] }, _service.Search(table, "Value", "*" + literal));
    }

    [Fact]
    public void Star_matches_empty_and_multiline_values()
    {
        var table = CreateTable(new StringType(), "", "\n", "😀\ntest\n");
        Assert.Equal(table.Rows, _service.Search(table, "Value", "*"));
        Assert.Same(table.Rows[2], Assert.Single(_service.Search(table, "Value", "*test*")));
    }

    [Fact]
    public void Preserves_spaces_in_text_patterns()
    {
        var table = CreateTable(new StringType(), "Dan", " Dan ");
        Assert.Same(table.Rows[1], Assert.Single(_service.Search(table, "Value", " Dan ")));
    }

    [Fact]
    public void Supports_long_literal_patterns_with_and_without_wildcards()
    {
        var literal = new string('x', 2000);
        var table = CreateTable(new StringType(), literal, "prefix" + literal, literal + "suffix");
        Assert.Same(table.Rows[0], Assert.Single(_service.Search(table, "Value", literal)));
        Assert.Equal(table.Rows, _service.Search(table, "Value", "*" + literal + "*"));
    }

    [Fact]
    public void Literal_parts_cannot_overlap()
    {
        var table = CreateTable(new StringType(), "abc", "abbc", "abXYZbc");
        Assert.Equal(new[] { table.Rows[1], table.Rows[2] }, _service.Search(table, "Value", "ab*bc"));
        Assert.Equal(new[] { table.Rows[1], table.Rows[2] }, _service.Search(table, "Value", "*ab*bc*"));
    }

    [Theory]
    [InlineData("D", new[] { "D" })]
    [InlineData("D*", new[] { "D" })]
    [InlineData("*", new[] { "D", "d", "ї", " " })]
    [InlineData("Dan*", new string[0])]
    [InlineData("", new string[0])]
    public void Char_uses_the_same_text_pattern_rules(string pattern, string[] expected)
    {
        var table = CreateTable(new CharType(), "D", "d", "ї", " ");
        Assert.Equal(expected,
            _service.Search(table, "Value", pattern).Select(row => row.GetValue("Value").ToString()));
    }

    [Fact]
    public void Integer_search_compares_parsed_values()
    {
        var table = CreateTable(new IntegerType(), "1", "+01", "2", "-1");
        Assert.Equal(new[] { table.Rows[0], table.Rows[1] }, _service.Search(table, "Value", "001"));
        Assert.Same(table.Rows[3], Assert.Single(_service.Search(table, "Value", "-1")));
        Assert.Empty(_service.Search(table, "Value", "99"));
    }

    [Fact]
    public void Real_search_uses_typed_equality_and_invariant_culture()
    {
        var table = CreateTable(new RealType(), "1.5", "1.500", "15e-1", "2.5");
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            Assert.Equal(table.Rows.Take(3), _service.Search(table, "Value", "1.5000"));
            Assert.Throws<FormatException>(() => _service.Search(table, "Value", "1,5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Color_search_compares_RGB_components()
    {
        var table = CreateTable(new ColorType(), "RGB(255,0,0)", "RGB(255, 0, 0)", "RGB(0, 255, 0)");
        Assert.Equal(new[] { table.Rows[0], table.Rows[1] },
            _service.Search(table, "Value", " RGB( 255 , 0 , 0 ) "));
        Assert.Empty(_service.Search(table, "Value", "RGB(0, 0, 255)"));
    }

    [Fact]
    public void Color_interval_search_compares_both_bounds_exactly()
    {
        var table = CreateTable(new ColorIntervalType(),
            "[RGB(10,20,30);RGB(100,120,150)]",
            "[RGB(10, 20, 30); RGB(100, 120, 150)]",
            "[RGB(0, 0, 0); RGB(255, 255, 255)]");

        Assert.Equal(new[] { table.Rows[0], table.Rows[1] },
            _service.Search(table, "Value", "[ RGB(10,20,30) ; RGB(100,120,150) ]"));
    }

    [Fact]
    public void Rejects_nonexistent_columns_and_uses_case_sensitive_names()
    {
        var table = CreateTable(new StringType(), "Dan");
        Assert.Throws<KeyNotFoundException>(() => _service.Search(table, "Missing", "*"));
        Assert.Throws<KeyNotFoundException>(() => _service.Search(table, "value", "*"));
    }

    [Fact]
    public void Rejects_null_table_or_pattern()
    {
        var table = CreateTable(new StringType(), "Dan");
        Assert.Throws<ArgumentNullException>(() => _service.Search(null!, "Value", "*"));
        Assert.Throws<ArgumentNullException>(() => _service.Search(table, "Value", null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_missing_column_names(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => _service.Search(CreateTable(new StringType()), name!, "*"));

    [Theory]
    [InlineData("integer", "*")]
    [InlineData("integer", "1.5")]
    [InlineData("real", "NaN")]
    [InlineData("real", "*")]
    [InlineData("color", "RGB(256, 0, 0)")]
    [InlineData("color", "*")]
    [InlineData("colorInvl", "[RGB(10, 0, 0); RGB(0, 0, 0)]")]
    [InlineData("colorInvl", "*")]
    public void Rejects_invalid_nontext_patterns_even_for_empty_tables(string typeName, string pattern)
    {
        IDataType type = typeName switch
        {
            "integer" => new IntegerType(),
            "real" => new RealType(),
            "color" => new ColorType(),
            "colorInvl" => new ColorIntervalType(),
            _ => throw new ArgumentException("Unknown test type.", nameof(typeName))
        };

        Assert.Throws<FormatException>(() => _service.Search(CreateTable(type), "Value", pattern));
    }

    [Fact]
    public void Valid_pattern_on_an_empty_table_returns_no_rows() =>
        Assert.Empty(_service.Search(CreateTable(new IntegerType()), "Value", "42"));

    [Fact]
    public void Search_preserves_table_rows_values_and_column_order()
    {
        var table = CreateTable(new StringType(), "Daniel", "Dan", "Other");
        var columns = table.Columns.ToArray();
        var rows = table.Rows.ToArray();
        var values = table.Rows.Select(row => row.Values.ToArray()).ToArray();

        var result = _service.Search(table, "Value", "Dan*");

        Assert.Equal(new[] { rows[0], rows[1] }, result);
        Assert.Equal(columns, table.Columns);
        Assert.Equal(rows, table.Rows);

        for (var index = 0; index < rows.Length; index++)
        {
            Assert.Equal(values[index], rows[index].Values.ToArray());
        }

        Assert.Throws<NotSupportedException>(() => ((IList<Row>)result).Clear());
        table.AddRow(["Danylo"]);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Searches_only_the_selected_column()
    {
        var table = new Table("Names");
        table.AddColumn(new Column("First", new StringType()));
        table.AddColumn(new Column("Second", new StringType()));
        table.AddRow(["Dan", "Other"]);
        table.AddRow(["Other", "Dan"]);

        Assert.Same(table.Rows[0], Assert.Single(_service.Search(table, "First", "Dan*")));
        Assert.Same(table.Rows[1], Assert.Single(_service.Search(table, "Second", "Dan*")));
    }

    private static Table CreateTable(IDataType type, params string[] values)
    {
        var table = new Table("Items");
        table.AddColumn(new Column("Value", type));

        foreach (var value in values)
        {
            table.AddRow([value]);
        }

        return table;
    }
}
