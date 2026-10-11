using System.Collections.ObjectModel;
using MiniDB.Desktop.Models;

namespace MiniDB.Desktop.ViewModels;

public sealed class CreateTableViewModel : ObservableObject
{
    private string _name = "";
    public static IReadOnlyList<string> DataTypeNames { get; } = ["integer", "real", "char", "string", "color", "colorInvl"];
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public ObservableCollection<ColumnDraft> Columns { get; } = [new() { Name = "Id", DataTypeName = "integer" }];
    public CreateTableRequest BuildRequest() => new(Name,
        Columns.Select(column => new ColumnRequest(column.Name, column.DataTypeName)).ToArray());
}
