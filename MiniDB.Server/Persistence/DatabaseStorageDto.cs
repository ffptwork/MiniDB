namespace MiniDB.Server.Persistence;

public sealed class DatabaseStorageDto
{
    public required string Name { get; init; }
    public required List<TableStorageDto> Tables { get; init; }
}
