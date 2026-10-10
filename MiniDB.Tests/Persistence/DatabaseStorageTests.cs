using System.Globalization;
using System.Text.Json;
using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Server.Persistence;

namespace MiniDB.Tests.Persistence;

public sealed class DatabaseStorageTests : IDisposable
{
    private readonly string _temporaryParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MiniDB.Persistence.Tests"));
    private readonly string _rootPath;
    private readonly DatabaseStorage _storage;

    public DatabaseStorageTests()
    {
        _rootPath = Path.Combine(_temporaryParent, Guid.NewGuid().ToString("N"));
        _storage = new DatabaseStorage(_rootPath);
    }

    [Fact]
    public async Task Roundtrips_an_empty_database()
    {
        await _storage.SaveAsync(new Database("Empty"));
        var loaded = await new DatabaseStorage(_rootPath).LoadAsync("Empty");
        Assert.Equal("Empty", loaded.Name);
        Assert.Empty(loaded.Tables);
    }

    [Fact]
    public async Task Roundtrips_a_table_with_schema_and_no_rows()
    {
        var database = CreateDatabase("Demo", new IntegerType());
        await _storage.SaveAsync(database);
        var table = Assert.Single((await _storage.LoadAsync("Demo")).Tables);
        Assert.Equal("Values", table.Name);
        Assert.IsType<IntegerType>(Assert.Single(table.Columns).DataType);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Roundtrips_all_six_types_and_multiple_rows()
    {
        var database = new Database("Demo");
        var table = new Table("TypedValues");
        table.AddColumn(new Column("Id", new IntegerType()));
        table.AddColumn(new Column("Amount", new RealType()));
        table.AddColumn(new Column("Letter", new CharType()));
        table.AddColumn(new Column("Text", new StringType()));
        table.AddColumn(new Column("Color", new ColorType()));
        table.AddColumn(new Column("Interval", new ColorIntervalType()));
        table.AddRow(["42", "0.10000000000000002", "ї", "", "RGB(255, 0, 0)",
            "[RGB(10, 20, 30); RGB(100, 120, 150)]"]);
        table.AddRow(["-1", "1.25e2", "\0", "\"quotes\"\\slash\n\t\r Привіт 😀\0", "RGB(0, 0, 0)",
            "[RGB(0, 0, 0); RGB(255, 255, 255)]"]);
        database.AddTable(table);

        await _storage.SaveAsync(database);
        var loaded = await _storage.LoadAsync("Demo");

        AssertDatabaseEqual(database, loaded);
        Assert.NotSame(database, loaded);
        Assert.NotSame(table.Rows[0], loaded.Tables[0].Rows[0]);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(FilePath("Demo")));
        var storedColumns = json.RootElement.GetProperty("Tables")[0].GetProperty("Columns");
        Assert.Equal(new[] { "integer", "real", "char", "string", "color", "colorInvl" },
            storedColumns.EnumerateArray().Select(column => column.GetProperty("DataTypeName").GetString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  spaced  ")]
    [InlineData("\"quoted\"\\path\n\t\r\0")]
    [InlineData("Привіт 😀")]
    public async Task Preserves_empty_and_special_strings(string value)
    {
        await _storage.SaveAsync(CreateDatabase("Text", new StringType(), value));
        var loaded = await _storage.LoadAsync("Text");
        Assert.Equal(value, loaded.Tables[0].Rows[0].GetValue("Value"));
    }

    [Theory]
    [InlineData(0.10000000000000002)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(-0.0)]
    public async Task Preserves_double_bits_and_uses_invariant_culture(double value)
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            await _storage.SaveAsync(CreateDatabase("Real", new RealType(), value.ToString("R", CultureInfo.InvariantCulture)));
            var loaded = await _storage.LoadAsync("Real");
            var actual = Assert.IsType<double>(loaded.Tables[0].Rows[0].GetValue("Value"));
            Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(actual));
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(FilePath("Real")));
            Assert.Equal(value.ToString("R", CultureInfo.InvariantCulture),
                json.RootElement.GetProperty("Tables")[0].GetProperty("Rows")[0].GetProperty("Values").GetProperty("Value").GetString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Preserves_multiple_tables_and_case_sensitive_column_names()
    {
        var database = new Database("Demo");
        var table = new Table("Items");
        table.AddColumn(new Column("Id", new IntegerType()));
        table.AddColumn(new Column("id", new StringType()));
        table.AddRow(["1", "text"]);
        database.AddTable(table);
        var second = new Table("items");
        second.AddColumn(new Column("Name", new StringType()));
        database.AddTable(second);

        await _storage.SaveAsync(database);
        AssertDatabaseEqual(database, await _storage.LoadAsync("Demo"));
    }

    [Fact]
    public async Task Exists_and_Delete_report_presence_and_missing_files()
    {
        Assert.False(_storage.Exists("Demo"));
        Assert.False(_storage.Delete("Demo"));
        await _storage.SaveAsync(new Database("Demo"));
        Assert.True(_storage.Exists("Demo"));
        Assert.True(_storage.Delete("Demo"));
        Assert.False(_storage.Exists("Demo"));
        Assert.False(_storage.Delete("Demo"));
    }

    [Fact]
    public async Task GetAll_loads_databases_in_name_order_and_ignores_temporary_files()
    {
        Assert.Empty(await _storage.GetAllAsync());
        await _storage.SaveAsync(CreateDatabase("Zeta", new IntegerType(), "2"));
        await _storage.SaveAsync(new Database("Alpha"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, ".leftover.tmp"), "incomplete");

        var loaded = await _storage.GetAllAsync();

        Assert.Equal(new[] { "Alpha", "Zeta" }, loaded.Select(database => database.Name));
        Assert.Equal(2, loaded[1].Tables[0].Rows[0].GetValue("Value"));
        Assert.Throws<NotSupportedException>(() => ((IList<Database>)loaded).Clear());
    }

    [Fact]
    public async Task Missing_database_throws_FileNotFoundException() =>
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.LoadAsync("Missing"));

    [Fact]
    public async Task Unknown_type_is_reported_with_the_original_cause()
    {
        await WriteSingleValueJson("unknown", "anything");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _storage.LoadAsync("Broken"));
        Assert.Contains("unknown", error.Message);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"Name\":\"Broken\",\"Tables\":null}")]
    [InlineData("{\"Name\":\"Broken\",\"Tables\":[null]}")]
    [InlineData("{\"Name\":\"Broken\",\"Name\":\"Broken\",\"Tables\":[]}")]
    [InlineData("{\"Name\":\"Broken\",\"Tables\":[],\"Unexpected\":true}")]
    public async Task Corrupt_or_incomplete_documents_throw_InvalidDataException(string json)
    {
        await File.WriteAllTextAsync(FilePath("Broken"), json);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _storage.LoadAsync("Broken"));
        Assert.NotNull(error.InnerException);
        Assert.Contains("Broken.json", error.Message);
    }

    [Theory]
    [InlineData("integer", "not an integer")]
    [InlineData("real", "1,5")]
    [InlineData("real", "NaN")]
    [InlineData("char", "ab")]
    [InlineData("color", "RGB(256, 0, 0)")]
    [InlineData("colorInvl", "[RGB(10, 0, 0); RGB(0, 0, 0)]")]
    public async Task Invalid_values_reuse_domain_parsers_and_are_reported(string type, string value)
    {
        await WriteSingleValueJson(type, value);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _storage.LoadAsync("Broken"));
        Assert.IsType<FormatException>(error.InnerException);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Value\":\"1\",\"Extra\":\"2\"}")]
    [InlineData("{\"Unknown\":\"1\"}")]
    [InlineData("{\"value\":\"1\"}")]
    [InlineData("{\"Value\":null}")]
    [InlineData("{\"Value\":1}")]
    [InlineData("{\"Value\":\"1\",\"Value\":\"2\"}")]
    public async Task Rejects_missing_extra_wrong_case_or_nontext_values(string values)
    {
        var json = "{\"Name\":\"Broken\",\"Tables\":[{\"Name\":\"Values\",\"Columns\":[{\"Name\":\"Value\",\"DataTypeName\":\"integer\"}],\"Rows\":[{\"Values\":" + values + "}]}]}";
        await File.WriteAllTextAsync(FilePath("Broken"), json);
        await Assert.ThrowsAsync<InvalidDataException>(() => _storage.LoadAsync("Broken"));
    }

    [Fact]
    public async Task Restores_values_in_schema_order_instead_of_JSON_property_order()
    {
        const string json = """
            {"Name":"Demo","Tables":[{"Name":"Items","Columns":[
              {"Name":"Id","DataTypeName":"integer"},{"Name":"Text","DataTypeName":"string"}],
              "Rows":[{"Values":{"Text":"hello","Id":"42"}}]}]}
            """;
        await File.WriteAllTextAsync(FilePath("Demo"), json);
        var loaded = await _storage.LoadAsync("Demo");
        Assert.Equal(42, loaded.Tables[0].Rows[0].GetValue("Id"));
        Assert.Equal("hello", loaded.Tables[0].Rows[0].GetValue("Text"));
    }

    [Fact]
    public async Task Rejects_a_database_name_that_does_not_match_its_file()
    {
        await File.WriteAllTextAsync(FilePath("Demo"), "{\"Name\":\"Other\",\"Tables\":[]}");
        await Assert.ThrowsAsync<InvalidDataException>(() => _storage.LoadAsync("Demo"));
    }

    [Fact]
    public async Task GetAll_does_not_hide_a_corrupt_database()
    {
        await _storage.SaveAsync(new Database("Good"));
        await File.WriteAllTextAsync(FilePath("Broken"), "{");
        await Assert.ThrowsAsync<InvalidDataException>(() => _storage.GetAllAsync());
    }

    [Fact]
    public async Task Save_replaces_the_previous_document_and_leaves_no_temporary_files()
    {
        var database = CreateDatabase("Demo", new IntegerType(), "1");
        await _storage.SaveAsync(database);
        database.Tables[0].EditRow(0, ["2"]);
        await _storage.SaveAsync(database);
        Assert.Equal(2, (await _storage.LoadAsync("Demo")).Tables[0].Rows[0].GetValue("Value"));
        Assert.Equal(new[] { "Demo.json" }, Directory.EnumerateFiles(_rootPath).Select(path => Path.GetFileName(path)));
        Assert.Contains("\n  \"Name\"", await File.ReadAllTextAsync(FilePath("Demo")));
    }

    [Fact]
    public async Task Invalid_save_preserves_the_existing_document()
    {
        var database = CreateDatabase("Demo", new IntegerType(), "1");
        await _storage.SaveAsync(database);
        var before = await File.ReadAllBytesAsync(FilePath("Demo"));
        database.AddTable(new Table("NoColumns"));

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(database));

        Assert.Equal(before, await File.ReadAllBytesAsync(FilePath("Demo")));
        Assert.Empty(Directory.EnumerateFiles(_rootPath, "*.tmp"));
    }

    [Fact]
    public async Task Cancelled_save_preserves_the_existing_document()
    {
        await _storage.SaveAsync(CreateDatabase("Demo", new IntegerType(), "1"));
        var before = await File.ReadAllBytesAsync(FilePath("Demo"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _storage.SaveAsync(CreateDatabase("Demo", new IntegerType(), "2"), cancellation.Token));

        Assert.Equal(before, await File.ReadAllBytesAsync(FilePath("Demo")));
        Assert.Empty(Directory.EnumerateFiles(_rootPath, "*.tmp"));
    }

    [Fact]
    public async Task Cancelled_load_and_listing_propagate_cancellation()
    {
        await _storage.SaveAsync(new Database("Demo"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _storage.LoadAsync("Demo", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _storage.GetAllAsync(cancellation.Token));
    }

    [Fact]
    public async Task Failed_replacement_cleans_up_the_temporary_file()
    {
        Directory.CreateDirectory(FilePath("Blocked"));
        var error = await Record.ExceptionAsync(() => _storage.SaveAsync(new Database("Blocked")));
        Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected exception: {error}");
        Assert.True(Directory.Exists(FilePath("Blocked")));
        Assert.Empty(Directory.EnumerateFiles(_rootPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("/absolute")]
    [InlineData("C:\\absolute")]
    [InlineData("folder/name")]
    [InlineData("folder\\name")]
    [InlineData("bad:name")]
    [InlineData("bad?name")]
    [InlineData("bad*name")]
    [InlineData("bad|name")]
    [InlineData("bad<name")]
    [InlineData("bad>name")]
    [InlineData("bad\"name")]
    [InlineData("bad\0name")]
    [InlineData("bad\nname")]
    [InlineData("name.")]
    [InlineData("name ")]
    [InlineData(" name")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    public async Task All_operations_reject_unsafe_database_names(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => _storage.Exists(name!));
        Assert.ThrowsAny<ArgumentException>(() => _storage.Delete(name!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _storage.LoadAsync(name!));

        if (!string.IsNullOrWhiteSpace(name))
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => _storage.SaveAsync(new Database(name)));
        }

        Assert.Empty(Directory.EnumerateFiles(_rootPath));
    }

    [Fact]
    public async Task Supports_unicode_spaces_and_dots_in_safe_names()
    {
        const string name = "Навчальна база.v1";
        await _storage.SaveAsync(new Database(name));
        Assert.True(_storage.Exists(name));
        Assert.Equal(name, (await _storage.LoadAsync(name)).Name);
    }

    [Fact]
    public async Task Case_collisions_do_not_overwrite_an_existing_database()
    {
        await _storage.SaveAsync(new Database("Demo"));
        var before = await File.ReadAllBytesAsync(FilePath("Demo"));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(new Database("demo")));
        Assert.Throws<ArgumentException>(() => _storage.Delete("demo"));
        Assert.Equal(before, await File.ReadAllBytesAsync(FilePath("Demo")));
    }

    [Fact]
    public async Task Concurrent_saves_produce_one_complete_document()
    {
        var saves = Enumerable.Range(0, 8)
            .Select(index => _storage.SaveAsync(CreateDatabase("Demo", new IntegerType(), index.ToString(CultureInfo.InvariantCulture))));
        await Task.WhenAll(saves);

        var loaded = await _storage.LoadAsync("Demo");
        Assert.InRange(Assert.IsType<int>(loaded.Tables[0].Rows[0].GetValue("Value")), 0, 7);
        Assert.Empty(Directory.EnumerateFiles(_rootPath, "*.tmp"));
    }

    [Fact]
    public async Task Null_database_and_missing_root_path_are_rejected()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.SaveAsync(null!));
        Assert.Throws<ArgumentNullException>(() => new DatabaseStorage(null!));
        Assert.Throws<ArgumentException>(() => new DatabaseStorage(" "));
    }

    private string FilePath(string name) => Path.Combine(_rootPath, name + ".json");

    private Task WriteSingleValueJson(string type, string value)
    {
        var dto = new DatabaseStorageDto
        {
            Name = "Broken",
            Tables = [new TableStorageDto
            {
                Name = "Values",
                Columns = [new ColumnStorageDto { Name = "Value", DataTypeName = type }],
                Rows = [new RowStorageDto { Values = new Dictionary<string, string> { ["Value"] = value } }]
            }]
        };

        return File.WriteAllTextAsync(FilePath("Broken"), JsonSerializer.Serialize(dto));
    }

    private static Database CreateDatabase(string name, IDataType type, params string[] values)
    {
        var database = new Database(name);
        var table = new Table("Values");
        table.AddColumn(new Column("Value", type));

        foreach (var value in values)
        {
            table.AddRow([value]);
        }

        database.AddTable(table);
        return database;
    }

    private static void AssertDatabaseEqual(Database expected, Database actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Tables.Count, actual.Tables.Count);

        for (var tableIndex = 0; tableIndex < expected.Tables.Count; tableIndex++)
        {
            var expectedTable = expected.Tables[tableIndex];
            var actualTable = actual.Tables[tableIndex];
            Assert.Equal(expectedTable.Name, actualTable.Name);
            Assert.Equal(expectedTable.Columns.Select(column => column.Name), actualTable.Columns.Select(column => column.Name));
            Assert.Equal(expectedTable.Columns.Select(column => column.DataType.Name), actualTable.Columns.Select(column => column.DataType.Name));
            Assert.Equal(expectedTable.Rows.Count, actualTable.Rows.Count);

            for (var rowIndex = 0; rowIndex < expectedTable.Rows.Count; rowIndex++)
            {
                foreach (var column in expectedTable.Columns)
                {
                    var expectedValue = expectedTable.Rows[rowIndex].GetValue(column.Name);
                    var actualValue = actualTable.Rows[rowIndex].GetValue(column.Name);
                    Assert.Equal(expectedValue.GetType(), actualValue.GetType());
                    Assert.Equal(expectedValue, actualValue);
                }
            }
        }
    }

    public void Dispose()
    {
        var resolvedRoot = Path.GetFullPath(_rootPath);

        if (!string.Equals(Path.GetDirectoryName(resolvedRoot), _temporaryParent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to remove a directory outside the temporary test parent.");
        }

        if (Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}
