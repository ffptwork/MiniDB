namespace MiniDB.Desktop.Models;

public sealed record TableDto(string Name, IReadOnlyList<ColumnDto> Columns, IReadOnlyList<RowDto> Rows);
