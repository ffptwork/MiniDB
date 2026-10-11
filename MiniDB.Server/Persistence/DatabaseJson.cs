using System.Text.Json;
using System.Text.Json.Serialization;
using MiniDB.Core.Models;

namespace MiniDB.Server.Persistence;

internal static class DatabaseJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    public static Task WriteAsync(Stream stream, Database database, CancellationToken token) =>
        JsonSerializer.SerializeAsync(stream, DatabaseStorageMapper.ToDto(database), Options, token);

    public static async Task<Database> ReadAsync(Stream stream, CancellationToken token)
    {
        try
        {
            var dto = await JsonSerializer.DeserializeAsync<DatabaseStorageDto>(stream, Options, token)
                ?? throw new JsonException("Документ бази даних не може бути null.");
            return DatabaseStorageMapper.FromDto(dto);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException
            or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidDataException($"Некоректний JSON бази даних: {exception.Message}", exception);
        }
    }
}
