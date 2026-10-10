namespace MiniDB.Server.Contracts;

public sealed record DatabaseResponse(string Name, IReadOnlyList<TableResponse> Tables);
