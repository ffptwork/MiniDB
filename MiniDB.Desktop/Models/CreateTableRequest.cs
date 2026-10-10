namespace MiniDB.Desktop.Models;

public sealed record CreateTableRequest(string Name, IReadOnlyList<ColumnRequest> Columns);
