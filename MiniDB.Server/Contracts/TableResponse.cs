namespace MiniDB.Server.Contracts;

public sealed record TableResponse(string Name, IReadOnlyList<ColumnResponse> Columns,
    IReadOnlyList<RowResponse> Rows);
