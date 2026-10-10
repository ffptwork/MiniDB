namespace MiniDB.Server.Persistence;

public sealed class RowStorageDto
{
    public required Dictionary<string, string> Values { get; init; }
}
