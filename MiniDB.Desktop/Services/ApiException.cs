using System.Net;

namespace MiniDB.Desktop.Services;

public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Title { get; }
    public string? Detail { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ApiException(HttpStatusCode statusCode, string title, string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(string.IsNullOrWhiteSpace(detail) ? title : $"{title}: {detail}")
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}
