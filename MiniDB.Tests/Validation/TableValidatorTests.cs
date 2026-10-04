using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Core.Validation;

namespace MiniDB.Tests.Validation;

public sealed class TableValidatorTests
{
    private readonly TableValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_table_with_all_six_types()
    {
        var table = new Table("Items");
        IDataType[] types = [new IntegerType(), new RealType(), new CharType(),
            new StringType(), new ColorType(), new ColorIntervalType()];

        foreach (var type in types)
        {
            table.AddColumn(new Column(type.Name, type));
        }

        _validator.Validate(table);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Rejects_missing_table_names(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => _validator.ValidateTableName(name!));

    [Fact]
    public void Rejects_a_table_without_columns() =>
        Assert.Throws<ArgumentException>(() => _validator.Validate(new Table("Empty")));

    [Fact]
    public void Rejects_duplicate_column_names()
    {
        Column[] columns = [new("Id", new IntegerType()), new("Id", new StringType())];
        var error = Assert.Throws<ArgumentException>(() => _validator.ValidateColumns(columns));
        Assert.Contains("Id", error.Message);
    }

    [Fact]
    public void Uses_case_sensitive_column_names() =>
        _validator.ValidateColumns([new("Id", new IntegerType()), new("id", new StringType())]);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejects_a_data_type_without_a_name(string? name)
    {
        var column = new Column("Value", new NamedStringType(name!));
        Assert.Throws<ArgumentException>(() => _validator.ValidateColumns([column]));
    }

    [Fact]
    public void Rejects_null_arguments_and_null_columns()
    {
        Assert.Throws<ArgumentNullException>(() => _validator.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => _validator.ValidateColumns(null!));
        Assert.Throws<ArgumentException>(() => _validator.ValidateColumns([null!]));
    }

    [Fact]
    public void Existing_column_constructor_rejects_missing_names_and_types()
    {
        // Invalid Columns cannot reach a validator through the public model API.
        Assert.Throws<ArgumentException>(() => new Column("", new StringType()));
        Assert.Throws<ArgumentException>(() => new Column("  ", new StringType()));
        Assert.Throws<ArgumentNullException>(() => new Column("Value", null!));
    }

    private sealed class NamedStringType(string name) : IDataType
    {
        private readonly StringType _inner = new();
        public string Name => name;
        public bool Validate(string? value) => _inner.Validate(value);
        public bool ValidateValue(object? value) => _inner.ValidateValue(value);
        public object Parse(string value) => _inner.Parse(value);
    }
}
