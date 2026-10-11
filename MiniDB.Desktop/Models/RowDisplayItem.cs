namespace MiniDB.Desktop.Models;

public sealed class RowDisplayItem
{
    public RowDto Row { get; }
    public int Index => Row.Index;
    public IReadOnlyList<string> Cells { get; }

    public RowDisplayItem(RowDto row, IReadOnlyList<ColumnDto> columns)
    {
        Row = row;
        Cells = columns.Select(column => row.Values[column.Name]).ToArray();
    }
}
