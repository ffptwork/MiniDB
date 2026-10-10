using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using MiniDB.Server;
using MiniDB.Server.Contracts;

namespace MiniDB.Tests.Api;

public abstract class ApiTestBase : IAsyncLifetime
{
    private readonly string _temporaryRoot = Path.Combine(Path.GetTempPath(), "MiniDB.Api.Tests");
    private readonly string _testRoot;
    private WebApplication? _app;
    protected HttpClient Client { get; private set; } = null!;
    protected string StoragePath { get; }
    protected const string TablesUrl = "/api/databases/Demo/tables";
    protected const string TableUrl = TablesUrl + "/People";
    protected const string RowsUrl = TableUrl + "/rows";

    protected ApiTestBase()
    {
        _testRoot = Path.Combine(_temporaryRoot, Guid.NewGuid().ToString("N"));
        StoragePath = Path.Combine(_testRoot, "storage");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_testRoot);
        _app = ServerApplication.Build([
            "--environment", "Testing", "--urls", "http://127.0.0.1:0",
            "--contentRoot", _testRoot, "--Storage:Path", StoragePath,
            "--Logging:LogLevel:Default", "None"
        ]);
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        Client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(_testRoot)),
                Path.GetFullPath(_temporaryRoot), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Тимчасова папка виходить за межі каталогу тестів.");
        }

        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    protected async Task CreateDatabaseAsync(string name = "Demo")
    {
        using var response = await Client.PostAsJsonAsync("/api/databases", new CreateDatabaseRequest { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    protected async Task CreateTableAsync()
    {
        await CreateDatabaseAsync();
        using var response = await Client.PostAsJsonAsync(TablesUrl, ValidTable());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    protected static CreateTableRequest ValidTable(string name = "People") => new()
    {
        Name = name,
        Columns = [
            new() { Name = "Id", DataTypeName = "integer" },
            new() { Name = "Name", DataTypeName = "string" },
            new() { Name = "Rating", DataTypeName = "real" },
            new() { Name = "Initial", DataTypeName = "char" },
            new() { Name = "Tint", DataTypeName = "color" },
            new() { Name = "Range", DataTypeName = "colorInvl" }
        ]
    };

    protected static Dictionary<string, string> Values(string name = "Daniel", string id = "1") => new()
    {
        ["Name"] = name, ["Id"] = id, ["Rating"] = "1.25", ["Initial"] = "D",
        ["Tint"] = "RGB(255, 0, 0)", ["Range"] = "[RGB(10, 20, 30); RGB(100, 120, 150)]"
    };

    protected async Task AddRowAsync(Dictionary<string, string> values)
    {
        using var response = await Client.PostAsJsonAsync(RowsUrl, new AddRowRequest { Values = values });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    protected async Task<TableResponse> GetTableAsync()
    {
        using var response = await Client.GetAsync(TableUrl);
        return await ReadAsync<TableResponse>(response);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(result);
        return result;
    }

    protected static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("title").GetString()));
        Assert.False(json.RootElement.TryGetProperty("stackTrace", out _));
    }
}
