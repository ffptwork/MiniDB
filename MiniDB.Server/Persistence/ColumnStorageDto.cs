namespace MiniDB.Server.Persistence;

public sealed class ColumnStorageDto
{
    public required string Name { get; init; }
    public required string DataTypeName { get; init; }
}
