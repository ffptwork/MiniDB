using System.Globalization;
using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Server.Persistence;

namespace MiniDB.Tests.Persistence;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "MiniDB.Persistence.Tests");
    private readonly string _path;
    private readonly DatabaseStorage _storage;

    public PersistenceTests()
    {
        _path = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
        _storage = new DatabaseStorage(_path);
    }

    [Fact]
    public async Task SaveLoad_RestoresDatabaseWithTable()
    {
        var database = new Database("Demo");
        var table = new Table("People");
        table.AddColumn(new Column("Name", new StringType()));
        table.AddRow(["Daniel"]);
        database.AddTable(table);
        await _storage.SaveAsync(database);
        var loaded = await new DatabaseStorage(_path).LoadAsync("Demo");
        Assert.Equal("Demo", loaded.Name);
        Assert.Equal("People", Assert.Single(loaded.Tables).Name);
        Assert.Equal("Daniel", loaded.Tables[0].Rows[0].GetValue("Name"));
    }

    [Fact]
    public async Task SaveLoad_PreservesAllSixTypesAndText()
    {
        var database = new Database("Typed");
        var table = new Table("Values");
        foreach (var name in new[] { "integer", "real", "char", "string", "color", "colorInvl" })
            table.AddColumn(new Column(name, DataTypeFactory.Create(name)));
        table.AddRow(["42", "1.25", "ї", "", "RGB(255, 0, 0)", "[RGB(10, 20, 30); RGB(100, 120, 150)]"]);
        table.AddRow(["-1", "0.5", "A", "Привіт \"quotes\"\n😀", "RGB(0, 0, 255)", "[RGB(0, 0, 0); RGB(255, 255, 255)]"]);
        database.AddTable(table);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            await _storage.SaveAsync(database);
            var loaded = (await _storage.LoadAsync("Typed")).Tables[0];
            Assert.Equal(table.Columns.Select(column => column.DataType.Name), loaded.Columns.Select(column => column.DataType.Name));
            Assert.Equal(2, loaded.Rows.Count);
            for (var index = 0; index < table.Rows.Count; index++)
                foreach (var column in table.Columns)
                    Assert.Equal(table.Rows[index].GetValue(column.Name), loaded.Rows[index].GetValue(column.Name));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    public void Dispose()
    {
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(_path)), Path.GetFullPath(_parent), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Неправильний шлях тимчасового сховища.");
        Directory.Delete(_path, recursive: true);
    }
}
