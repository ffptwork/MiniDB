namespace MiniDB.Desktop.ViewModels;

public sealed class ColumnDraft : ObservableObject
{
    private string _name = "";
    private string _dataTypeName = "string";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataTypeName { get => _dataTypeName; set => SetProperty(ref _dataTypeName, value); }
}
