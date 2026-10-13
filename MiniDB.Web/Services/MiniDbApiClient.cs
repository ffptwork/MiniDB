using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using MiniDB.Web.Models;

namespace MiniDB.Web.Services;

public sealed class MiniDbApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public MiniDbApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var address = httpClient.BaseAddress
            ?? throw new ArgumentException("Задайте HttpClient.BaseAddress перед створенням клієнта.", nameof(httpClient));
        if (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps
            || !address.AbsolutePath.EndsWith('/') || address.Query.Length != 0 || address.Fragment.Length != 0)
        {
            throw new ArgumentException("BaseAddress має бути HTTP/HTTPS-адресою без query та fragment і завершуватися '/'.",
                nameof(httpClient));
        }

        _httpClient = httpClient;
    }

    public Task<IReadOnlyList<DatabaseDto>> GetDatabasesAsync(CancellationToken cancellationToken = default) =>
        SendForResultAsync<IReadOnlyList<DatabaseDto>>(HttpMethod.Get, "api/databases", null, cancellationToken);

    public Task<DatabaseDto> CreateDatabaseAsync(string name, CancellationToken cancellationToken = default) =>
        SendForResultAsync<DatabaseDto>(HttpMethod.Post, "api/databases", new CreateDatabaseRequest(name), cancellationToken);

    public Task<DatabaseDto> GetDatabaseAsync(string name, CancellationToken cancellationToken = default) =>
        SendForResultAsync<DatabaseDto>(HttpMethod.Get, DatabasePath(name), null, cancellationToken);

    public async Task DeleteDatabaseAsync(string name, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, DatabasePath(name), null, cancellationToken);
    }

    public Task<TableDto> GetTableAsync(string databaseName, string tableName, CancellationToken cancellationToken = default) =>
        SendForResultAsync<TableDto>(HttpMethod.Get, TablePath(databaseName, tableName), null, cancellationToken);

    public Task<TableDto> CreateTableAsync(string databaseName, CreateTableRequest request,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<TableDto>(HttpMethod.Post, DatabasePath(databaseName) + "/tables", request, cancellationToken);

    public async Task DeleteTableAsync(string databaseName, string tableName, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, TablePath(databaseName, tableName), null, cancellationToken);
    }

    public Task<RowDto> AddRowAsync(string databaseName, string tableName, Dictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<RowDto>(HttpMethod.Post, TablePath(databaseName, tableName) + "/rows",
            new RowValuesRequest(values), cancellationToken);

    public Task<RowDto> EditRowAsync(string databaseName, string tableName, int rowIndex, Dictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<RowDto>(HttpMethod.Put,
            TablePath(databaseName, tableName) + "/rows/" + rowIndex.ToString(CultureInfo.InvariantCulture),
            new RowValuesRequest(values), cancellationToken);

    public Task<IReadOnlyList<RowDto>> SearchAsync(string databaseName, string tableName, string columnName, string pattern,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<IReadOnlyList<RowDto>>(HttpMethod.Get,
            TablePath(databaseName, tableName) + "/search?columnName=" + Escape(columnName) + "&pattern=" + Escape(pattern),
            null, cancellationToken);

    public async Task<byte[]> ExportDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, DatabasePath(databaseName) + "/export", null, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<DatabaseDto> ImportDatabaseAsync(Stream file, string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", fileName);
        using var response = await SendContentAsync(HttpMethod.Post, "api/databases/import", content, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DatabaseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер повернув порожню JSON-відповідь.");
    }

    private async Task<T> SendForResultAsync<T>(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, path, body, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер повернув порожню JSON-відповідь.");
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken) =>
        SendContentAsync(method, path, body is null ? null : JsonContent.Create(body, options: JsonOptions), cancellationToken);

    private async Task<HttpResponseMessage> SendContentAsync(HttpMethod method, string path, HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        try
        {
            if (!response.IsSuccessStatusCode)
                await ThrowApiExceptionAsync(response, cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task ThrowApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ProblemDetailsDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Проксі може повернути HTML чи порожню відповідь замість ProblemDetails.
        }

        var title = string.IsNullOrWhiteSpace(problem?.Title)
            ? $"Помилка API (HTTP {(int)response.StatusCode})." : problem.Title;
        throw new ApiException(response.StatusCode, title, problem?.Detail, problem?.Errors);
    }

    private static string DatabasePath(string name) => "api/databases/" + Escape(name);

    private static string TablePath(string databaseName, string tableName) =>
        DatabasePath(databaseName) + "/tables/" + Escape(tableName);

    private static string Escape(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Uri.EscapeDataString(name);
    }
}
