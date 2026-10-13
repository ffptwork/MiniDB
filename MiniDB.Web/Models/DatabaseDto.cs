namespace MiniDB.Web.Models;

public sealed record DatabaseDto(string Name, IReadOnlyList<TableDto> Tables);
