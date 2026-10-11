using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MiniDB.Desktop.Services;

public static class UserErrorFormatter
{
    public static string Format(Exception exception) => exception switch
    {
        ApiException api => FormatApi(api),
        HttpRequestException => "Не вдалося підключитися до сервера. Перевірте, чи сервер запущено та чи правильно задано ApiBaseUrl.",
        OperationCanceledException => "Запит не завершився вчасно. Спробуйте ще раз.",
        JsonException or InvalidDataException => "Сервер повернув некоректну відповідь.",
        IOException or UnauthorizedAccessException => "Не вдалося прочитати або записати файл. Перевірте шлях і права доступу.",
        _ => "Не вдалося виконати операцію. Спробуйте ще раз."
    };

    private static string FormatApi(ApiException exception)
    {
        var lines = new List<string> { exception.Title };
        if (!string.IsNullOrWhiteSpace(exception.Detail)) lines.Add(exception.Detail);
        foreach (var (field, messages) in exception.Errors)
        {
            lines.AddRange(messages.Select(message => $"{field}: {message}"));
        }
        return string.Join(Environment.NewLine, lines.Distinct(StringComparer.Ordinal));
    }
}
