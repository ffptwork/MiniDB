using System.Text.Json;
using System.Text.Json.Serialization;
using MiniDB.Core.Models;

namespace MiniDB.Server.Persistence;

public sealed class DatabaseStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private readonly string _rootPath;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public DatabaseStorage(string rootStoragePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootStoragePath);
        _rootPath = Path.GetFullPath(rootStoragePath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task SaveAsync(Database database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            await SaveFileAsync(database, cancellationToken);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task SaveFileAsync(Database database, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filePath = GetDatabasePath(database.Name);
        var dto = DatabaseStorageMapper.ToDto(database);
        var temporaryPath = Path.Combine(_rootPath, $".{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public async Task<Database> LoadAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        var filePath = GetDatabasePath(databaseName);
        cancellationToken.ThrowIfCancellationRequested();

        // Keep the old file readable if a concurrent save replaces its directory entry.
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous);

        try
        {
            var dto = await JsonSerializer.DeserializeAsync<DatabaseStorageDto>(stream, JsonOptions, cancellationToken)
                ?? throw new JsonException("The database document must not be null.");

            if (!string.Equals(dto.Name, databaseName, StringComparison.Ordinal))
            {
                throw new ArgumentException("The database name in JSON must match its file name.");
            }

            return DatabaseStorageMapper.FromDto(dto);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException
            or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidDataException($"Invalid database file '{filePath}': {exception.Message}", exception);
        }
    }

    public bool Exists(string databaseName) => File.Exists(GetDatabasePath(databaseName));

    public bool Delete(string databaseName)
    {
        var filePath = GetDatabasePath(databaseName);

        if (!File.Exists(filePath))
        {
            return false;
        }

        File.Delete(filePath);
        return true;
    }

    /// <summary>Loads every database in name order; invalid files are reported, not skipped.</summary>
    public async Task<IReadOnlyList<Database>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var names = Directory.EnumerateFiles(_rootPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var databases = new List<Database>();

        foreach (var name in names)
        {
            databases.Add(await LoadAsync(name, cancellationToken));
        }

        return databases.AsReadOnly();
    }

    private string GetDatabasePath(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        if (databaseName is "." or ".." || databaseName != databaseName.Trim()
            || databaseName.EndsWith('.') || Path.IsPathRooted(databaseName)
            || databaseName.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0
            || databaseName.Any(char.IsControl)
            || ReservedNames.Contains(databaseName.Split('.')[0]))
        {
            throw new ArgumentException("Database name must be a safe file name without paths, invalid characters or reserved names.", nameof(databaseName));
        }

        var fileName = databaseName + ".json";
        var conflictingFile = Directory.EnumerateFiles(_rootPath)
            .Select(path => Path.GetFileName(path))
            .FirstOrDefault(existing => string.Equals(existing, fileName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(existing, fileName, StringComparison.Ordinal));

        if (conflictingFile is not null)
        {
            throw new ArgumentException($"Database file name conflicts with '{conflictingFile}' by letter case.", nameof(databaseName));
        }

        return Path.Combine(_rootPath, fileName);
    }
}
