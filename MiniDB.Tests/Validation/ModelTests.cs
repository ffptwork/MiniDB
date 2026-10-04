using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

namespace MiniDB.Tests.Validation;

public sealed class ModelTests
{
    [Fact]
    public void Database_can_add_get_and_remove_tables()
    {
        var database = new Database("MiniDB");
        var table = new Table("Items");
        database.AddTable(table);
        Assert.Equal("MiniDB", database.Name);
        Assert.Same(table, database.GetTable("Items"));
        Assert.Single(database.Tables);
        Assert.True(database.RemoveTable("Items"));
        Assert.Empty(database.Tables);
        Assert.False(database.RemoveTable("Items"));
        Assert.Throws<KeyNotFoundException>(() => database.GetTable("Items"));
    }

    [Fact]
    public void Duplicate_names_are_rejected_and_matching_is_case_sensitive()
    {
        var database = new Database("MiniDB");
        database.AddTable(new Table("Items"));
        Assert.Throws<ArgumentException>(() => database.AddTable(new Table("Items")));
        database.AddTable(new Table("items"));
        Assert.Equal(2, database.Tables.Count);

        var table = new Table("Data");
        var column = new Column("Id", new IntegerType());
        table.AddColumn(column);
        Assert.Same(column, table.GetColumn("Id"));
        Assert.Throws<ArgumentException>(() => table.AddColumn(new Column("Id", new IntegerType())));
        Assert.Throws<KeyNotFoundException>(() => table.GetColumn("id"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Models_reject_missing_names(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Database(name!));
        Assert.ThrowsAny<ArgumentException>(() => new Table(name!));
        Assert.ThrowsAny<ArgumentException>(() => new Column(name!, new StringType()));
    }

    [Fact]
    public void Models_reject_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new Column("Id", null!));
        Assert.Throws<ArgumentNullException>(() => new Database("DB").AddTable(null!));
        Assert.Throws<ArgumentNullException>(() => new Table("Items").AddColumn(null!));
    }

    [Fact]
    public void Table_stores_values_as_all_six_column_types()
    {
        var table = new Table("TypedValues");
        table.AddColumn(new Column("Id", new IntegerType()));
        table.AddColumn(new Column("Amount", new RealType()));
        table.AddColumn(new Column("Letter", new CharType()));
        table.AddColumn(new Column("Text", new StringType()));
        table.AddColumn(new Column("Color", new ColorType()));
        table.AddColumn(new Column("Interval", new ColorIntervalType()));

        var row = table.AddRow(["42", "1.5", "ї", "", "RGB(10, 20, 30)",
            "[RGB(0, 0, 0); RGB(255, 255, 255)]"]);

        Assert.Equal(42, Assert.IsType<int>(row.GetValue("Id")));
        Assert.Equal(1.5d, Assert.IsType<double>(row.GetValue("Amount")));
        Assert.Equal('ї', Assert.IsType<char>(row.GetValue("Letter")));
        Assert.Equal("", Assert.IsType<string>(row.GetValue("Text")));
        Assert.Equal(new Color(10, 20, 30), Assert.IsType<Color>(row.GetValue("Color")));
        Assert.IsType<ColorInterval>(row.GetValue("Interval"));
        Assert.Same(row, Assert.Single(table.GetRows()));
    }

    [Fact]
    public void Invalid_insert_does_not_add_a_row()
    {
        var table = CreateTable();
        Assert.Throws<FormatException>(() => table.AddRow(["1", "bad number"]));
        Assert.Throws<ArgumentException>(() => table.AddRow(["1"]));
        Assert.Throws<ArgumentException>(() => table.AddRow(["1", "2.5", "extra"]));
        Assert.Throws<ArgumentNullException>(() => table.AddRow(null!));
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void Cell_edit_validates_type_and_preserves_old_value_on_failure()
    {
        var table = CreateTable();
        var row = table.AddRow(["1", "2.5"]);
        row.SetValue("Id", "3");
        Assert.Equal(3, row.GetValue("Id"));
        Assert.Throws<FormatException>(() => row.SetValue("Id", "bad number"));
        Assert.Throws<ArgumentNullException>(() => row.SetValue("Id", null!));
        Assert.Equal(3, row.GetValue("Id"));
        Assert.Throws<KeyNotFoundException>(() => row.GetValue("Missing"));
        Assert.Throws<KeyNotFoundException>(() => row.SetValue("Missing", "1"));
    }

    [Fact]
    public void Row_edit_is_atomic_and_preserves_row_identity()
    {
        var table = CreateTable();
        var row = table.AddRow(["1", "2.5"]);
        Assert.Throws<FormatException>(() => table.EditRow(0, ["99", "invalid"]));
        Assert.Throws<ArgumentException>(() => table.EditRow(0, ["99"]));
        Assert.Equal(1, row.GetValue("Id"));
        Assert.Equal(2.5d, row.GetValue("Amount"));

        table.EditRow(0, ["3", "4.5"]);
        Assert.Same(row, table.Rows[0]);
        Assert.Equal(3, row.GetValue("Id"));
        Assert.Equal(4.5d, row.GetValue("Amount"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Row_edit_rejects_out_of_range_index(int index)
    {
        var table = CreateTable();
        table.AddRow(["1", "2.5"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => table.EditRow(index, ["3", "4.5"]));
    }

    [Fact]
    public void Schema_must_be_defined_before_rows()
    {
        var table = new Table("Items");
        Assert.Throws<InvalidOperationException>(() => table.AddRow([]));
        table.AddColumn(new Column("Id", new IntegerType()));
        var row = table.AddRow(["1"]);
        Assert.Throws<InvalidOperationException>(() => table.AddColumn(new Column("Text", new StringType())));
        Assert.Single(table.Columns);
        Assert.Single(row.Values);
    }

    [Fact]
    public void Standalone_row_checks_its_schema()
    {
        var column = new Column("Id", new IntegerType());
        Assert.Throws<ArgumentException>(() => new Row([], []));
        Assert.Throws<ArgumentException>(() => new Row([column, column], ["1", "2"]));
        Assert.Throws<ArgumentNullException>(() => new Row(null!, ["1"]));
        Assert.Throws<ArgumentNullException>(() => new Row([null!], ["1"]));
    }

    [Fact]
    public void Collections_cannot_bypass_model_validation()
    {
        var database = new Database("DB");
        var table = CreateTable();
        database.AddTable(table);
        var rows = table.GetRows();
        var row = table.AddRow(["1", "2.5"]);
        Assert.Single(rows);
        Assert.Throws<NotSupportedException>(() => ((IList<Table>)database.Tables).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Column>)table.Columns).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Row>)table.Rows).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object>)row.Values)["Id"] = "bad value");
    }

    private static Table CreateTable()
    {
        var table = new Table("Items");
        table.AddColumn(new Column("Id", new IntegerType()));
        table.AddColumn(new Column("Amount", new RealType()));
        return table;
    }
}
