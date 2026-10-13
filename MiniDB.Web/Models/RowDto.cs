namespace MiniDB.Web.Models;

public sealed record RowDto(int Index, IReadOnlyDictionary<string, string> Values);
