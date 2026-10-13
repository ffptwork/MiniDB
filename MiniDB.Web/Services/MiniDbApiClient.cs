using System.Net.Http.Json;
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

    private async Task<T> SendForResultAsync<T>(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, path, body, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер повернув порожню JSON-відповідь.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body, options: JsonOptions)
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

    private static string DatabasePath(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return "api/databases/" + Uri.EscapeDataString(name);
    }
}
