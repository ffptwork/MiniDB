using System.IO;
using System.Text.Json;

namespace MiniDB.Desktop.Services;

public sealed class DesktopSettings
{
    public required string ApiBaseUrl { get; init; }

    public static DesktopSettings Load(string path) =>
        JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("Файл налаштувань порожній.");
}
