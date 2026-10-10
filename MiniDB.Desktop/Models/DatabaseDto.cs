namespace MiniDB.Desktop.Models;

public sealed record DatabaseDto(string Name, IReadOnlyList<TableDto> Tables);
