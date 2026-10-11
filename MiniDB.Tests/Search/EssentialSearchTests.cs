using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Core.Search;

namespace MiniDB.Tests.Search;

public sealed class EssentialSearchTests
{
    private static Table Values(IDataType type, params string[] values)
    {
        var table = new Table("Values");
        table.AddColumn(new Column("Value", type));
        foreach (var value in values) table.AddRow([value]);
        return table;
    }

    [Fact]
    public void Search_DanStarMatchesPrefixAndPreservesRows()
    {
        var table = Values(new StringType(), "Dan", "Daniel", "Danylo", "dan", "Anna");
        var result = new SearchService().Search(table, "Value", "Dan*");
        Assert.Equal(["Dan", "Daniel", "Danylo"], result.Select(row => (string)row.GetValue("Value")));
        Assert.Equal(5, table.Rows.Count);
        Assert.Same(table.Rows[1], result[1]);
    }

    [Fact]
    public void Search_StarsMatchTextAnywhere()
    {
        var table = Values(new StringType(), "test", "pretestpost", "TEST", "other");
        Assert.Equal(["test", "pretestpost"], new SearchService().Search(table, "Value", "*test*")
            .Select(row => (string)row.GetValue("Value")));
    }

    [Fact]
    public void Search_IntegerUsesTypedExactMatch()
    {
        var table = Values(new IntegerType(), "7", "70", "8");
        Assert.Same(table.Rows[0], Assert.Single(new SearchService().Search(table, "Value", "+007")));
    }

    [Fact]
    public void Search_ColorUsesTypedExactMatch()
    {
        var table = Values(new ColorType(), "RGB(255, 0, 0)", "RGB(0, 0, 255)");
        Assert.Same(table.Rows[0], Assert.Single(new SearchService().Search(table, "Value", "RGB(255,0,0)")));
    }

    [Fact]
    public void Search_HandlesNoResultsAndMissingColumn()
    {
        var table = Values(new StringType(), "Daniel");
        var service = new SearchService();
        Assert.Empty(service.Search(table, "Value", "missing*"));
        Assert.Throws<KeyNotFoundException>(() => service.Search(table, "Unknown", "*"));
    }
}
