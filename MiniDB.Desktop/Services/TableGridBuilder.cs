using MiniDB.Desktop.Models;

namespace MiniDB.Desktop.Services;

public sealed record GridColumnDefinition(string Header, string DataTypeName, string BindingPath);

public static class TableGridBuilder
{
    public static IReadOnlyList<GridColumnDefinition> BuildColumns(IReadOnlyList<ColumnDto> columns) =>
        columns.Select((column, index) =>
            new GridColumnDefinition(column.Name, column.DataTypeName, $"Cells[{index}]")).ToArray();

    public static IReadOnlyList<RowDisplayItem> BuildRows(IReadOnlyList<ColumnDto> columns, IEnumerable<RowDto> rows) =>
        rows.Select(row => new RowDisplayItem(row, columns)).ToArray();
}
