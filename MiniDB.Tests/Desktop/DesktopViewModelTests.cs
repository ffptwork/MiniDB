using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MiniDB.Desktop.Models;
using MiniDB.Desktop.Services;
using MiniDB.Desktop.ViewModels;

namespace MiniDB.Tests.Desktop;

public sealed class DesktopViewModelTests
{
    [Fact]
    public async Task Refresh_LoadsDatabasesAndSelectsFirstDatabaseAndTable()
    {
        using var fixture = new Fixture();
        Assert.True(await fixture.Model.RefreshAsync());
        Assert.Equal(["Demo", "Other"], fixture.Model.Databases.Select(database => database.Name));
        Assert.Equal("Demo", fixture.Model.SelectedDatabase?.Name);
        Assert.Equal("People", fixture.Model.CurrentTable?.Name);
        Assert.Equal(8, fixture.Model.Rows.Count);
        Assert.False(fixture.Model.IsBusy);
    }

    [Fact]
    public async Task SelectDatabase_LoadsItsTablesAndClearsOldRows()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        Assert.True(await fixture.Model.SelectDatabaseAsync(fixture.Model.Databases[1]));
        Assert.Equal("Other", fixture.Model.SelectedDatabase?.Name);
        Assert.Empty(fixture.Model.Tables);
        Assert.Empty(fixture.Model.Rows);
        Assert.Null(fixture.Model.CurrentTable);
        Assert.False(fixture.Model.CanEditRow);
    }

    [Fact]
    public async Task SelectTable_FetchesSchemaAndCreatesGridData()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        Assert.True(await fixture.Model.SelectTableAsync(fixture.Model.Tables[0]));
        Assert.Contains(fixture.Requests, request => request == "GET /api/databases/Demo/tables/People");
        Assert.Equal(["Id", "Name"], fixture.Model.GridColumns.Select(column => column.Header));
        Assert.Equal(["Cells[0]", "Cells[1]"], fixture.Model.GridColumns.Select(column => column.BindingPath));
        Assert.Equal(["0", "Person 0"], fixture.Model.Rows[0].Cells);
        Assert.Equal("Id : integer   •   Name : string", fixture.Model.SchemaSummary);
    }

    [Fact]
    public void GridBuilder_HandlesSpecialColumnNamesAndRetainsOriginalRowIndex()
    {
        ColumnDto[] columns = [new("Name] /#", "string"), new("name", "string")];
        var row = new RowDto(12, new Dictionary<string, string> { ["Name] /#"] = "A", ["name"] = "B" });
        var definitions = TableGridBuilder.BuildColumns(columns);
        Assert.Equal("Name] /#", definitions[0].Header);
        Assert.Equal("Cells[0]", definitions[0].BindingPath);
        var displayed = Assert.Single(TableGridBuilder.BuildRows(columns, [row]));
        Assert.Equal(12, displayed.Index);
        Assert.Equal(["A", "B"], displayed.Cells);
        Assert.Same(row, displayed.Row);
    }

    [Fact]
    public void CreateTableDraft_BuildsIndependentRequestWithAllSixTypes()
    {
        var model = new CreateTableViewModel { Name = "People" };
        model.Columns.Clear();
        foreach (var type in CreateTableViewModel.DataTypeNames)
            model.Columns.Add(new ColumnDraft { Name = type + "Column", DataTypeName = type });
        var request = model.BuildRequest();
        Assert.Equal("People", request.Name);
        Assert.Equal(["integer", "real", "char", "string", "color", "colorInvl"], request.Columns.Select(column => column.DataTypeName));
        model.Columns[0].Name = "Changed";
        Assert.Equal("integerColumn", request.Columns[0].Name);
    }

    [Fact]
    public void RowEditor_PreservesEmptyAndSpecialTextWithoutParsing()
    {
        ColumnDto[] columns = [new("Name", "string"), new("name", "string"), new("Tint", "color")];
        var model = new RowEditorViewModel(columns);
        Assert.All(model.Fields, field => Assert.Equal("", field.Value));
        model.Fields[1].Value = "Текст \"quotes\"\n😀";
        model.Fields[2].Value = "invalid RGB";
        var values = model.BuildValues();
        Assert.Equal("", values["Name"]);
        Assert.Equal("Текст \"quotes\"\n😀", values["name"]);
        Assert.Equal("invalid RGB", values["Tint"]);
        model.Fields[1].Value = "Changed";
        Assert.Equal("Текст \"quotes\"\n😀", values["name"]);
    }

    [Fact]
    public void RowEditor_PrefillsExistingValuesAndProvidesTypeHints()
    {
        ColumnDto[] columns = [new("Tint", "color"), new("Range", "colorInvl")];
        var row = new RowDto(7, new Dictionary<string, string>
        {
            ["Tint"] = "RGB(255, 0, 0)", ["Range"] = "[RGB(10, 20, 30); RGB(100, 120, 150)]"
        });
        var model = new RowEditorViewModel(columns, row);
        Assert.Equal("RGB(255, 0, 0)", model.Fields[0].Value);
        Assert.Equal("Tint (color)", model.Fields[0].Label);
        Assert.Contains("[RGB(", model.Fields[1].Hint);
    }

    [Fact]
    public async Task Search_UsesApiAndRetainsOriginalIndices()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        fixture.Model.SearchColumn = fixture.Model.Columns[1];
        fixture.Model.Pattern = "Dan*";
        Assert.True(await fixture.Model.SearchAsync());
        Assert.Equal(7, Assert.Single(fixture.Model.Rows).Index);
        Assert.True(fixture.Model.IsSearchActive);
        Assert.Contains("GET /api/databases/Demo/tables/People/search?columnName=Name&pattern=Dan%2A", fixture.Requests);
        Assert.Contains("результати пошуку", fixture.Model.Status);
        Assert.Contains("Dan*", fixture.Model.SearchHint);
    }

    [Fact]
    public async Task ResetSearch_ReloadsAllRowsAndClearsPatternAndSelection()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        fixture.Model.Pattern = "*";
        await fixture.Model.SearchAsync();
        fixture.Model.SelectedRow = fixture.Model.Rows[0];
        Assert.True(await fixture.Model.ResetSearchAsync());
        Assert.Equal(8, fixture.Model.Rows.Count);
        Assert.Equal("", fixture.Model.Pattern);
        Assert.False(fixture.Model.IsSearchActive);
        Assert.Null(fixture.Model.SelectedRow);
    }

    [Fact]
    public async Task EditFilteredRow_UsesDtoIndexAndRefreshesTable()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        await fixture.Model.SearchAsync();
        fixture.Model.SelectedRow = fixture.Model.Rows[0];
        Assert.True(fixture.Model.CanEditRow);
        Assert.True(await fixture.Model.EditRowAsync(fixture.Model.SelectedRow.Index,
            new Dictionary<string, string> { ["Id"] = "7", ["Name"] = "Daniel" }));
        Assert.Contains("PUT /api/databases/Demo/tables/People/rows/7", fixture.Requests);
        Assert.Equal(8, fixture.Model.Rows.Count);
    }

    [Fact]
    public async Task CreateDatabase_RefreshesAndSelectsCreatedDatabase()
    {
        using var fixture = new Fixture();
        Assert.True(await fixture.Model.CreateDatabaseAsync("New"));
        Assert.Equal("New", fixture.Model.SelectedDatabase?.Name);
        Assert.Contains(fixture.Model.Databases, database => database.Name == "New");
    }

    [Fact]
    public async Task CreateTable_RefreshesAndSelectsCreatedTable()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        Assert.True(await fixture.Model.CreateTableAsync(new CreateTableRequest("NewTable", [new("Id", "integer")])));
        Assert.Equal("NewTable", fixture.Model.CurrentTable?.Name);
        Assert.Contains(fixture.Model.Tables, table => table.Name == "NewTable");
    }

    [Fact]
    public async Task AddRow_PostsTextValuesAndReloadsTable()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        Assert.True(await fixture.Model.AddRowAsync(new Dictionary<string, string> { ["Id"] = "8", ["Name"] = "New" }));
        Assert.Contains("POST /api/databases/Demo/tables/People/rows", fixture.Requests);
        Assert.Equal(9, fixture.Model.Rows.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_RefreshesSelectionAndClearsRemovedData(bool database)
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        if (database)
        {
            Assert.True(await fixture.Model.DeleteDatabaseAsync());
            Assert.Equal("Other", fixture.Model.SelectedDatabase?.Name);
            Assert.Empty(fixture.Model.Rows);
        }
        else
        {
            Assert.True(await fixture.Model.DeleteTableAsync());
            Assert.Empty(fixture.Model.Tables);
            Assert.Null(fixture.Model.CurrentTable);
        }
    }

    [Fact]
    public async Task BusyState_PreventsOverlappingActionsAndReturnsToNormal()
    {
        using var fixture = new Fixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeRequest = async token => { started.TrySetResult(); await release.Task.WaitAsync(token); };
        var loading = fixture.Model.RefreshAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(fixture.Model.IsBusy);
        Assert.False(fixture.Model.CanInteract);
        Assert.Equal("Завантаження...", fixture.Model.Status);
        Assert.False(await fixture.Model.RefreshAsync());
        release.TrySetResult();
        Assert.True(await loading);
        Assert.False(fixture.Model.IsBusy);
        Assert.Equal(1, fixture.Requests.Count(request => request == "GET /api/databases"));
    }

    [Fact]
    public async Task CancelledLoad_DoesNotDisplayCriticalError()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.False(await fixture.Model.RefreshAsync(cancellation.Token));
        Assert.Equal("", fixture.Model.ErrorMessage);
        Assert.False(fixture.Model.IsBusy);
    }

    [Fact]
    public async Task ConnectionFailure_ShowsFriendlyMessageAndRestoresActions()
    {
        using var fixture = new Fixture();
        fixture.BeforeRequest = _ => throw new HttpRequestException("Sensitive technical details");
        Assert.False(await fixture.Model.RefreshAsync());
        Assert.Contains("Не вдалося підключитися до сервера", fixture.Model.ErrorMessage);
        Assert.DoesNotContain("Sensitive", fixture.Model.ErrorMessage);
        Assert.True(fixture.Model.CanInteract);
    }

    [Fact]
    public async Task SavedChangeWithFailedReload_IsStillReportedAsSavedToAvoidDuplicateSubmission()
    {
        using var fixture = new Fixture();
        await fixture.Model.RefreshAsync();
        fixture.FailReloadAfterWrite = true;
        Assert.True(await fixture.Model.AddRowAsync(new Dictionary<string, string> { ["Id"] = "8", ["Name"] = "New" }));
        Assert.Contains("Зміни збережено", fixture.Model.ErrorMessage);
        Assert.Equal(1, fixture.Requests.Count(request => request.StartsWith("POST ", StringComparison.Ordinal)));
    }

    [Fact]
    public void ErrorFormatter_IncludesTitleDetailAndFieldErrors()
    {
        var exception = new ApiException(HttpStatusCode.BadRequest, "Некоректні дані.", "Перевірте значення.",
            new Dictionary<string, string[]> { ["Id"] = ["Потрібне ціле число."] });
        var text = UserErrorFormatter.Format(exception);
        Assert.Contains("Некоректні дані.", text);
        Assert.Contains("Перевірте значення.", text);
        Assert.Contains("Id: Потрібне ціле число.", text);
        Assert.DoesNotContain(nameof(ApiException), text);
    }

    [Fact]
    public void Settings_ReadsApiBaseUrlFromExternalJson()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"ApiBaseUrl\":\"https://example.test/\"}");
            Assert.Equal("https://example.test/", DesktopSettings.Load(path).ApiBaseUrl);
        }
        finally { File.Delete(path); }
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
        private readonly HttpClient _http;
        private readonly List<DatabaseDto> _databases;
        private TableDto _table;
        public MainViewModel Model { get; }
        public List<string> Requests { get; } = [];
        public Func<CancellationToken, Task>? BeforeRequest { get; set; }
        public bool FailReloadAfterWrite { get; set; }
        private bool _wrote;

        public Fixture()
        {
            _table = new("People", [new("Id", "integer"), new("Name", "string")],
                Enumerable.Range(0, 8).Select(index => new RowDto(index, new Dictionary<string, string>
                { ["Id"] = index.ToString(), ["Name"] = "Person " + index })).ToArray());
            _databases = [new("Demo", [_table]), new("Other", [])];
            _http = new HttpClient(new StubHttpMessageHandler(ReplyAsync)) { BaseAddress = new Uri("http://example.test/") };
            Model = new MainViewModel(new MiniDbApiClient(_http));
        }

        private async Task<HttpResponseMessage> ReplyAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (BeforeRequest is not null) await BeforeRequest(token);
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(request.Method + " " + request.RequestUri.PathAndQuery);
            if (FailReloadAfterWrite && _wrote && request.Method == HttpMethod.Get) throw new HttpRequestException("Reload failed");
            object result;
            if (request.Method == HttpMethod.Get)
            {
                if (path.EndsWith("/search", StringComparison.Ordinal)) result = new[] { _table.Rows[7] };
                else if (path == "/api/databases") result = _databases;
                else if (path.Contains("/tables/", StringComparison.Ordinal)) result = _table;
                else result = _databases.Single(database => database.Name == path.Split('/')[3]);
            }
            else if (request.Method == HttpMethod.Delete)
            {
                if (path.Contains("/tables/", StringComparison.Ordinal)) _databases[0] = _databases[0] with { Tables = [] };
                else _databases.RemoveAt(0);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            else
            {
                _wrote = true;
                if (path.EndsWith("/rows", StringComparison.Ordinal) || request.Method == HttpMethod.Put)
                {
                    using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                    var values = json.RootElement.GetProperty("values").Deserialize<Dictionary<string, string>>(Options)!;
                    var row = new RowDto(request.Method == HttpMethod.Put ? int.Parse(path.Split('/').Last()) : _table.Rows.Count, values);
                    if (request.Method == HttpMethod.Post) _table = _table with { Rows = _table.Rows.Append(row).ToArray() };
                    result = row;
                }
                else if (path.EndsWith("/tables", StringComparison.Ordinal))
                {
                    var dto = (await request.Content!.ReadFromJsonAsync<CreateTableRequest>(Options, token))!;
                    _table = new(dto.Name, dto.Columns.Select(column => new ColumnDto(column.Name, column.DataTypeName)).ToArray(), []);
                    _databases[0] = _databases[0] with { Tables = [_table] };
                    result = _table;
                }
                else
                {
                    var dto = (await request.Content!.ReadFromJsonAsync<CreateDatabaseRequest>(Options, token))!;
                    var database = new DatabaseDto(dto.Name, []);
                    _databases.Add(database);
                    result = database;
                }
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(result, Options), Encoding.UTF8, "application/json") };
        }

        public void Dispose() => _http.Dispose();
    }
}
