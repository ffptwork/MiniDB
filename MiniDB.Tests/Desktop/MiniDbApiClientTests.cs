using System.Net;
using System.Text;
using System.Text.Json;
using MiniDB.Desktop.Models;
using MiniDB.Desktop.Services;

namespace MiniDB.Tests.Desktop;

public sealed class MiniDbApiClientTests
{
    private const string DatabaseJson = "{\"name\":\"Demo\",\"tables\":[]}";
    private const string TableJson = """
        {"name":"People","columns":[
          {"name":"Id","dataTypeName":"integer"},
          {"name":"Name","dataTypeName":"string"},
          {"name":"Rating","dataTypeName":"real"},
          {"name":"Initial","dataTypeName":"char"},
          {"name":"Tint","dataTypeName":"color"},
          {"name":"Range","dataTypeName":"colorInvl"}
        ],"rows":[{"index":7,"values":{
          "Id":"1","Name":"","Rating":"1.25","Initial":"D",
          "Tint":"RGB(255, 0, 0)","Range":"[RGB(10, 20, 30); RGB(100, 120, 150)]"
        }}]}
        """;
    private const string RowJson = "{\"index\":7,\"values\":{\"Id\":\"1\",\"Name\":\"Daniel\"}}";

    [Theory]
    [InlineData("list", "GET", "api/databases")]
    [InlineData("create-database", "POST", "api/databases")]
    [InlineData("get-database", "GET", "api/databases/Demo")]
    [InlineData("delete-database", "DELETE", "api/databases/Demo")]
    [InlineData("get-table", "GET", "api/databases/Demo/tables/People")]
    [InlineData("create-table", "POST", "api/databases/Demo/tables")]
    [InlineData("delete-table", "DELETE", "api/databases/Demo/tables/People")]
    [InlineData("add-row", "POST", "api/databases/Demo/tables/People/rows")]
    [InlineData("edit-row", "PUT", "api/databases/Demo/tables/People/rows/7")]
    [InlineData("search", "GET", "api/databases/Demo/tables/People/search?columnName=Name&pattern=Dan%2A")]
    public async Task Operations_UseExpectedMethodAndUrl(string operation, string method, string path)
    {
        using var fixture = new ClientFixture(ResponseFor(operation));
        await InvokeAsync(fixture.Api, operation);
        Assert.Equal(method, fixture.Method);
        Assert.Equal("http://localhost:5143/" + path, fixture.Url?.AbsoluteUri);
        Assert.Equal(1, fixture.RequestCount);
        if (method is "GET" or "DELETE") Assert.Null(fixture.Body);
    }

    [Theory]
    [InlineData("create-database")]
    [InlineData("create-table")]
    [InlineData("add-row")]
    [InlineData("edit-row")]
    public async Task Writes_SerializeExpectedRequestContract(string operation)
    {
        using var fixture = new ClientFixture(ResponseFor(operation));
        await InvokeAsync(fixture.Api, operation);
        Assert.Equal("application/json", fixture.ContentType);
        using var json = JsonDocument.Parse(fixture.Body!);
        var root = json.RootElement;
        if (operation == "create-database")
        {
            Assert.Equal("Demo", root.GetProperty("name").GetString());
            Assert.Single(root.EnumerateObject());
        }
        else if (operation == "create-table")
        {
            Assert.Equal("People", root.GetProperty("name").GetString());
            var columns = root.GetProperty("columns");
            Assert.Equal(2, columns.GetArrayLength());
            Assert.Equal("Id", columns[0].GetProperty("name").GetString());
            Assert.Equal("integer", columns[0].GetProperty("dataTypeName").GetString());
            Assert.Equal("Name", columns[1].GetProperty("name").GetString());
            Assert.Equal("string", columns[1].GetProperty("dataTypeName").GetString());
            Assert.Equal(2, root.EnumerateObject().Count());
        }
        else
        {
            var values = root.GetProperty("values");
            Assert.Equal(6, values.EnumerateObject().Count());
            foreach (var (name, value) in Values())
            {
                Assert.Equal(JsonValueKind.String, values.GetProperty(name).ValueKind);
                Assert.Equal(value, values.GetProperty(name).GetString());
            }
            Assert.Single(root.EnumerateObject());
        }
    }

    [Fact]
    public async Task GetDatabase_DeserializesNestedTablesColumnsAndRows()
    {
        using var fixture = new ClientFixture("{\"name\":\"Demo\",\"tables\":[" + TableJson + "]}");
        var database = await fixture.Api.GetDatabaseAsync("Demo");
        Assert.Equal("Demo", database.Name);
        AssertTable(Assert.Single(database.Tables));
    }

    [Fact]
    public async Task GetDatabases_DeserializesList()
    {
        using var fixture = new ClientFixture("[" + DatabaseJson + ", {\"name\":\"Other\",\"tables\":[]}]");
        var databases = await fixture.Api.GetDatabasesAsync();
        Assert.Equal(["Demo", "Other"], databases.Select(database => database.Name));
    }

    [Fact]
    public async Task GetTable_DeserializesAllSixTypeNamesAndTextValues()
    {
        using var fixture = new ClientFixture(TableJson);
        AssertTable(await fixture.Api.GetTableAsync("Demo", "People"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RowOperations_DeserialiseRowAndOriginalIndex(bool edit)
    {
        using var fixture = new ClientFixture(RowJson);
        var row = edit
            ? await fixture.Api.EditRowAsync("Demo", "People", 7, Values())
            : await fixture.Api.AddRowAsync("Demo", "People", Values());
        Assert.Equal(7, row.Index);
        Assert.Equal("1", row.Values["Id"]);
        Assert.Equal("Daniel", row.Values["Name"]);
    }

    [Fact]
    public async Task Search_DeserializesMatchesAndPreservesTableIndices()
    {
        using var fixture = new ClientFixture("[" + RowJson + "]");
        var row = Assert.Single(await fixture.Api.SearchAsync("Demo", "People", "Name", "Dan*"));
        Assert.Equal(7, row.Index);
        Assert.Equal("Daniel", row.Values["Name"]);
    }

    [Fact]
    public async Task Search_EscapesEveryPathAndQueryParameter()
    {
        using var fixture = new ClientFixture("[]");
        await fixture.Api.SearchAsync("My DB", "A/B?#%+", "Name &x", "Dan* &?#+/%");
        Assert.Equal("http://localhost:5143/api/databases/My%20DB/tables/A%2FB%3F%23%25%2B/search"
            + "?columnName=Name%20%26x&pattern=Dan%2A%20%26%3F%23%2B%2F%25", fixture.Url?.AbsoluteUri);
        Assert.Equal("", fixture.Url?.Fragment);
    }

    [Fact]
    public async Task Search_EscapesUnicodeAndAllowsEmptyPattern()
    {
        using var fixture = new ClientFixture("[]");
        await fixture.Api.SearchAsync("База", "Люди", "Ім'я", "");
        Assert.Contains("%D0%91", fixture.Url!.AbsoluteUri);
        Assert.Equal("/api/databases/База/tables/Люди/search", Uri.UnescapeDataString(fixture.Url.AbsolutePath));
        Assert.Equal("?columnName=Ім'я&pattern=", Uri.UnescapeDataString(fixture.Url.Query));
    }

    [Fact]
    public async Task BaseAddress_PreservesConfiguredPathPrefix()
    {
        using var fixture = new ClientFixture(DatabaseJson, baseAddress: "https://example.test/gateway/");
        await fixture.Api.GetDatabaseAsync("Demo");
        Assert.Equal("https://example.test/gateway/api/databases/Demo", fixture.Url?.AbsoluteUri);
    }

    [Theory]
    [InlineData(400, "Некоректні дані.")]
    [InlineData(404, "Об'єкт не знайдено.")]
    [InlineData(409, "База даних вже існує.")]
    [InlineData(500, "Внутрішня помилка сервера.")]
    public async Task Errors_ExposeProblemDetailsAndActualHttpStatus(int status, string title)
    {
        var json = JsonSerializer.Serialize(new { status = 418, title, detail = "Докладне повідомлення" });
        using var fixture = new ClientFixture(json, (HttpStatusCode)status);
        var exception = await Assert.ThrowsAsync<ApiException>(() => fixture.Api.GetDatabaseAsync("Demo"));
        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
        Assert.Equal(title, exception.Title);
        Assert.Equal("Докладне повідомлення", exception.Detail);
        Assert.Contains(title, exception.Message);
        Assert.Contains("Докладне повідомлення", exception.Message);
    }

    [Fact]
    public async Task ValidationProblemDetails_PreservesFieldErrorsWithoutDetail()
    {
        using var fixture = new ClientFixture("""
            {"title":"Некоректні дані запиту.","status":400,"errors":{"Name":["Ім'я обов'язкове."]}}
            """, HttpStatusCode.BadRequest);
        var exception = await Assert.ThrowsAsync<ApiException>(() => fixture.Api.CreateDatabaseAsync(""));
        Assert.Null(exception.Detail);
        Assert.Equal(exception.Title, exception.Message);
        Assert.Equal("Ім'я обов'язкове.", Assert.Single(exception.Errors["Name"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>Proxy error</html>")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task InvalidErrorBody_StillThrowsApiException(string body)
    {
        using var fixture = new ClientFixture(body, HttpStatusCode.BadGateway);
        var exception = await Assert.ThrowsAsync<ApiException>(() => fixture.Api.GetDatabasesAsync());
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Equal("Помилка API (HTTP 502).", exception.Title);
        Assert.Null(exception.Detail);
    }

    [Fact]
    public async Task NullSuccessBody_IsReportedAsInvalidResponse()
    {
        using var fixture = new ClientFixture("null");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Api.GetDatabaseAsync("Demo"));
    }

    [Fact]
    public async Task TransportFailure_IsNotConvertedToApiException()
    {
        var expected = new HttpRequestException("Сервер недоступний.");
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) => throw expected))
        {
            BaseAddress = new Uri("http://localhost:5143/")
        };
        var api = new MiniDbApiClient(httpClient);
        var actual = await Assert.ThrowsAsync<HttpRequestException>(() => api.GetDatabasesAsync());
        Assert.Same(expected, actual);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("create-database")]
    [InlineData("get-database")]
    [InlineData("delete-database")]
    [InlineData("get-table")]
    [InlineData("create-table")]
    [InlineData("delete-table")]
    [InlineData("add-row")]
    [InlineData("edit-row")]
    [InlineData("search")]
    public async Task Operations_RespectAlreadyCancelledToken(string operation)
    {
        using var fixture = new ClientFixture(ResponseFor(operation));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeAsync(fixture.Api, operation, cancellation.Token));
        Assert.Equal(0, fixture.RequestCount);
    }

    [Fact]
    public async Task Cancellation_InterruptsAnInFlightRequest()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("http://localhost:5143/") };
        var api = new MiniDbApiClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        var request = api.GetDatabasesAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    public async Task Cancellation_InterruptsReadingSuccessOrProblemBodyAndDisposesIt(int status)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var content = new DelayedHttpContent(started);
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = content })))
        {
            BaseAddress = new Uri("http://localhost:5143/")
        };
        var api = new MiniDbApiClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        var request = api.GetDatabasesAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.True(content.WasDisposed);
    }

    [Theory]
    [InlineData("ftp://example.test/")]
    [InlineData("http://example.test/prefix")]
    [InlineData("http://example.test/?query=value")]
    [InlineData("http://example.test/#fragment")]
    public void InvalidBaseAddress_IsRejected(string baseAddress)
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
        Assert.Throws<ArgumentException>(() => new MiniDbApiClient(httpClient));
    }

    [Fact]
    public void MissingBaseAddress_IsRejected()
    {
        using var httpClient = new HttpClient();
        Assert.Throws<ArgumentException>(() => new MiniDbApiClient(httpClient));
        Assert.Throws<ArgumentNullException>(() => new MiniDbApiClient(null!));
    }

    private static void AssertTable(TableDto table)
    {
        Assert.Equal("People", table.Name);
        Assert.Equal(["integer", "string", "real", "char", "color", "colorInvl"],
            table.Columns.Select(column => column.DataTypeName));
        var row = Assert.Single(table.Rows);
        Assert.Equal(7, row.Index);
        Assert.Equal("", row.Values["Name"]);
        Assert.Equal("1.25", row.Values["Rating"]);
        Assert.Equal("D", row.Values["Initial"]);
        Assert.Equal("RGB(255, 0, 0)", row.Values["Tint"]);
        Assert.Equal("[RGB(10, 20, 30); RGB(100, 120, 150)]", row.Values["Range"]);
    }

    private static Dictionary<string, string> Values() => new()
    {
        ["Id"] = "1", ["Name"] = "Тест \"quotes\" \\ newline\n😀",
        ["Rating"] = "1.25", ["Initial"] = "D", ["Tint"] = "RGB(255, 0, 0)",
        ["Range"] = "[RGB(10, 20, 30); RGB(100, 120, 150)]"
    };

    private static Task InvokeAsync(MiniDbApiClient api, string operation, CancellationToken token = default) => operation switch
    {
        "list" => api.GetDatabasesAsync(token),
        "create-database" => api.CreateDatabaseAsync("Demo", token),
        "get-database" => api.GetDatabaseAsync("Demo", token),
        "delete-database" => api.DeleteDatabaseAsync("Demo", token),
        "get-table" => api.GetTableAsync("Demo", "People", token),
        "create-table" => api.CreateTableAsync("Demo", new CreateTableRequest("People", [
            new ColumnRequest("Id", "integer"), new ColumnRequest("Name", "string")]), token),
        "delete-table" => api.DeleteTableAsync("Demo", "People", token),
        "add-row" => api.AddRowAsync("Demo", "People", Values(), token),
        "edit-row" => api.EditRowAsync("Demo", "People", 7, Values(), token),
        "search" => api.SearchAsync("Demo", "People", "Name", "Dan*", token),
        _ => throw new ArgumentException("Невідома тестова операція.", nameof(operation))
    };

    private static string ResponseFor(string operation) => operation switch
    {
        "list" => "[]",
        "get-table" or "create-table" => TableJson,
        "add-row" or "edit-row" => RowJson,
        "search" => "[]",
        _ => DatabaseJson
    };

    private sealed class ClientFixture : IDisposable
    {
        private readonly HttpClient _httpClient;
        public MiniDbApiClient Api { get; }
        public int RequestCount { get; private set; }
        public string? Method { get; private set; }
        public Uri? Url { get; private set; }
        public string? Body { get; private set; }
        public string? ContentType { get; private set; }

        public ClientFixture(string response, HttpStatusCode status = HttpStatusCode.OK,
            string baseAddress = "http://localhost:5143/")
        {
            _httpClient = new HttpClient(new StubHttpMessageHandler(async (request, token) =>
            {
                RequestCount++;
                Method = request.Method.Method;
                Url = request.RequestUri;
                ContentType = request.Content?.Headers.ContentType?.MediaType;
                Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
                return new HttpResponseMessage(request.Method == HttpMethod.Delete && status == HttpStatusCode.OK
                    ? HttpStatusCode.NoContent : status)
                {
                    Content = new StringContent(request.Method == HttpMethod.Delete && status == HttpStatusCode.OK
                        ? "" : response, Encoding.UTF8,
                        (int)status >= 400 ? "application/problem+json" : "application/json")
                };
            })) { BaseAddress = new Uri(baseAddress) };
            Api = new MiniDbApiClient(_httpClient);
        }

        public void Dispose() => _httpClient.Dispose();
    }

    private sealed class DelayedHttpContent(TaskCompletionSource started) : HttpContent
    {
        public bool WasDisposed { get; private set; }

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Stream.Null;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new NotSupportedException();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
