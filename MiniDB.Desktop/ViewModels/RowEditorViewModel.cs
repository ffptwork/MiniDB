using MiniDB.Desktop.Models;

namespace MiniDB.Desktop.ViewModels;

public sealed class RowEditorViewModel
{
    public IReadOnlyList<RowFieldViewModel> Fields { get; }
    public RowEditorViewModel(IReadOnlyList<ColumnDto> columns, RowDto? row = null)
    {
        Fields = columns.Select(column => new RowFieldViewModel(column.Name, column.DataTypeName,
            row?.Values[column.Name] ?? "")).ToArray();
    }
    public Dictionary<string, string> BuildValues() => Fields.ToDictionary(field => field.Name,
        field => field.Value, StringComparer.Ordinal);
}
