namespace MiniDB.Web.Models;

public sealed record CreateTableRequest(string Name, IReadOnlyList<ColumnRequest> Columns);
