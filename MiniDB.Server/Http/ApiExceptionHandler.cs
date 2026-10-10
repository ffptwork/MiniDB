using Microsoft.AspNetCore.Diagnostics;

namespace MiniDB.Server.Http;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            FileNotFoundException => (404, "Базу даних не знайдено.", (string?)null),
            KeyNotFoundException => (404, "Об'єкт не знайдено.", exception.Message),
            ArgumentException or FormatException => (400, "Некоректні дані.", exception.Message),
            _ => (500, "Внутрішня помилка сервера.", (string?)null)
        };

        if (status == 500)
        {
            logger.LogError(exception, "Помилка обробки запиту {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        await Results.Problem(statusCode: status, title: title, detail: detail)
            .ExecuteAsync(context);
        return true;
    }
}
