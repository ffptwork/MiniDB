namespace MiniDB.Server.Contracts;

public sealed record RowResponse(int Index, IReadOnlyDictionary<string, string> Values);
