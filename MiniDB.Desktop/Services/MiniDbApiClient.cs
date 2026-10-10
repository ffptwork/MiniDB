using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using MiniDB.Desktop.Models;

namespace MiniDB.Desktop.Services;

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

    public Task DeleteDatabaseAsync(string name, CancellationToken cancellationToken = default) =>
        DeleteAsync(DatabasePath(name), cancellationToken);

    public Task<TableDto> GetTableAsync(string databaseName, string tableName, CancellationToken cancellationToken = default) =>
        SendForResultAsync<TableDto>(HttpMethod.Get, TablePath(databaseName, tableName), null, cancellationToken);

    public Task<TableDto> CreateTableAsync(string databaseName, CreateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendForResultAsync<TableDto>(HttpMethod.Post, DatabasePath(databaseName) + "/tables", request, cancellationToken);
    }

    public Task DeleteTableAsync(string databaseName, string tableName, CancellationToken cancellationToken = default) =>
        DeleteAsync(TablePath(databaseName, tableName), cancellationToken);

    public Task<RowDto> AddRowAsync(string databaseName, string tableName, Dictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<RowDto>(HttpMethod.Post, TablePath(databaseName, tableName) + "/rows",
            CreateRowRequest(values), cancellationToken);

    public Task<RowDto> EditRowAsync(string databaseName, string tableName, int rowIndex, Dictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<RowDto>(HttpMethod.Put,
            TablePath(databaseName, tableName) + "/rows/" + rowIndex.ToString(CultureInfo.InvariantCulture),
            CreateRowRequest(values), cancellationToken);

    public Task<IReadOnlyList<RowDto>> SearchAsync(string databaseName, string tableName, string columnName, string pattern,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<IReadOnlyList<RowDto>>(HttpMethod.Get,
            TablePath(databaseName, tableName) + "/search?columnName=" + Escape(columnName) + "&pattern=" + Escape(pattern),
            null, cancellationToken);

    private async Task<T> SendForResultAsync<T>(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, path, body, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Сервер повернув порожню JSON-відповідь.");
    }

    private async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, path, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (!response.IsSuccessStatusCode)
            {
                await ThrowApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
            }

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
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Проксі або сервер може повернути порожню відповідь чи HTML замість ProblemDetails.
        }

        var title = string.IsNullOrWhiteSpace(problem?.Title)
            ? $"Помилка API (HTTP {(int)response.StatusCode})." : problem.Title;
        throw new ApiException(response.StatusCode, title, problem?.Detail, problem?.Errors);
    }

    private static string DatabasePath(string name) => "api/databases/" + Escape(name);
    private static string TablePath(string databaseName, string tableName) =>
        DatabasePath(databaseName) + "/tables/" + Escape(tableName);

    private static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Uri.EscapeDataString(value);
    }

    private static RowValuesRequest CreateRowRequest(Dictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new RowValuesRequest(new Dictionary<string, string>(values, StringComparer.Ordinal));
    }
}
