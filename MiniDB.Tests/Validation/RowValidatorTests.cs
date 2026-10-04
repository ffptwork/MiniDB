using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Core.Validation;

namespace MiniDB.Tests.Validation;

public sealed class RowValidatorTests
{
    private readonly RowValidator _validator = new();

    [Fact]
    public void Accepts_typed_values_for_all_six_types()
    {
        var table = new Table("Values");
        table.AddColumn(new Column("Id", new IntegerType()));
        table.AddColumn(new Column("Amount", new RealType()));
        table.AddColumn(new Column("Letter", new CharType()));
        table.AddColumn(new Column("Text", new StringType()));
        table.AddColumn(new Column("Color", new ColorType()));
        table.AddColumn(new Column("Interval", new ColorIntervalType()));
        var row = table.AddRow(["1", "1.5", "ї", "", "RGB(10, 20, 30)",
            "[RGB(0, 0, 0); RGB(255, 255, 255)]"]);

        _validator.Validate(table, row);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("not an integer")]
    public void Rejects_a_string_in_an_integer_column_even_if_text_can_be_parsed(string value)
    {
        var table = CreateTable(new Column("Id", new IntegerType()));
        var row = new Row([new Column("Id", new StringType())], [value]);

        var error = Assert.Throws<ArgumentException>(() => _validator.Validate(table, row));
        Assert.Contains("Id", error.Message);
        Assert.Contains("integer", error.Message);
        Assert.Equal(value, row.GetValue("Id"));
    }

    [Fact]
    public void Rejects_a_missing_value()
    {
        var table = CreateTable(new Column("Id", new IntegerType()), new Column("Name", new StringType()));
        var row = new Row([table.GetColumn("Id")], ["1"]);

        var error = Assert.Throws<ArgumentException>(() => _validator.Validate(table, row));
        Assert.Contains("missing", error.Message);
        Assert.Contains("Name", error.Message);
    }

    [Fact]
    public void Rejects_an_extra_value()
    {
        var table = CreateTable(new Column("Id", new IntegerType()));
        var row = new Row([table.GetColumn("Id"), new Column("Extra", new StringType())], ["1", "extra"]);

        var error = Assert.Throws<ArgumentException>(() => _validator.Validate(table, row));
        Assert.Contains("unknown", error.Message);
        Assert.Contains("Extra", error.Message);
    }

    [Fact]
    public void Rejects_unknown_names_even_when_value_count_matches()
    {
        var table = CreateTable(new Column("Id", new IntegerType()));
        var row = new Row([new Column("id", new IntegerType())], ["1"]);

        var error = Assert.Throws<ArgumentException>(() => _validator.Validate(table, row));
        Assert.Contains("unknown", error.Message);
    }

    [Fact]
    public void Checks_the_target_type_instead_of_only_the_column_name()
    {
        var table = CreateTable(new Column("Amount", new RealType()));
        var row = new Row([new Column("Amount", new IntegerType())], ["1"]);

        Assert.Throws<ArgumentException>(() => _validator.Validate(table, row));
    }

    [Fact]
    public void Does_not_reparse_stored_values()
    {
        var type = new CountingIntegerType();
        var table = CreateTable(new Column("Id", type));
        var row = table.AddRow(["42"]);
        var parseCalls = type.ParseCalls;

        _validator.Validate(table, row);

        Assert.Equal(parseCalls, type.ParseCalls);
        Assert.Equal(0, type.TextValidationCalls);
        Assert.Equal(1, type.TypedValidationCalls);
    }

    [Fact]
    public void Rejects_null_arguments_and_an_empty_target_schema()
    {
        var table = CreateTable(new Column("Id", new IntegerType()));
        var row = table.AddRow(["1"]);
        Assert.Throws<ArgumentNullException>(() => _validator.Validate(null!, row));
        Assert.Throws<ArgumentNullException>(() => _validator.Validate(table, null!));
        Assert.Throws<ArgumentException>(() => _validator.Validate(new Table("Empty"), row));
    }

    private static Table CreateTable(params Column[] columns)
    {
        var table = new Table("Items");

        foreach (var column in columns)
        {
            table.AddColumn(column);
        }

        return table;
    }

    private sealed class CountingIntegerType : IDataType
    {
        private readonly IntegerType _inner = new();
        public string Name => "integer";
        public int ParseCalls { get; private set; }
        public int TextValidationCalls { get; private set; }
        public int TypedValidationCalls { get; private set; }

        public bool Validate(string? value)
        {
            TextValidationCalls++;
            return _inner.Validate(value);
        }

        public bool ValidateValue(object? value)
        {
            TypedValidationCalls++;
            return _inner.ValidateValue(value);
        }

        public object Parse(string value)
        {
            ParseCalls++;
            return _inner.Parse(value);
        }
    }
}
