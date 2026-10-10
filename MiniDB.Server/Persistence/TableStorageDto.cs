namespace MiniDB.Server.Persistence;

public sealed class TableStorageDto
{
    public required string Name { get; init; }
    public required List<ColumnStorageDto> Columns { get; init; }
    public required List<RowStorageDto> Rows { get; init; }
}
