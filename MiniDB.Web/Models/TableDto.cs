namespace MiniDB.Web.Models;

public sealed record TableDto(string Name, IReadOnlyList<ColumnDto> Columns, IReadOnlyList<RowDto> Rows);
