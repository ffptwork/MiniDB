using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Core.Validation;

namespace MiniDB.Tests.Validation;

public sealed class ValidatorTests
{
    private static Table People()
    {
        var table = new Table("People");
        table.AddColumn(new Column("Id", new IntegerType()));
        table.AddColumn(new Column("Name", new StringType()));
        return table;
    }

    [Fact]
    public void TableValidator_AcceptsValidTable()
    {
        new TableValidator().Validate(People());
    }

    [Fact]
    public void TableValidator_RejectsDuplicateColumns()
    {
        Column[] columns = [new("Id", new IntegerType()), new("Id", new StringType())];
        Assert.Throws<ArgumentException>(() => new TableValidator().ValidateColumns(columns));
    }

    [Fact]
    public void RowValidator_AcceptsRowMatchingSchema()
    {
        var table = People();
        new RowValidator().Validate(table, table.AddRow(["1", "Daniel"]));
    }

    [Fact]
    public void RowValidator_RejectsValueOfWrongLogicalType()
    {
        Column[] columns = [new("Id", new StringType()), new("Name", new StringType())];
        var row = new Row(columns, ["not an integer", "Daniel"]);
        Assert.Throws<ArgumentException>(() => new RowValidator().Validate(People(), row));
    }
}
