namespace MiniDB.Desktop.ViewModels;

public sealed class RowFieldViewModel(string name, string dataTypeName, string value) : ObservableObject
{
    private string _value = value;
    public string Name { get; } = name;
    public string DataTypeName { get; } = dataTypeName;
    public string Label => $"{Name} ({DataTypeName})";
    public string Hint => DataTypeName switch
    {
        "integer" => "Наприклад: 42",
        "real" => "Десятковий роздільник — крапка. Наприклад: 1.25",
        "char" => "Один символ",
        "color" => "RGB(255, 0, 0)",
        "colorInvl" => "[RGB(10, 20, 30); RGB(100, 120, 150)]",
        _ => "Довільний текст; рядок може бути порожнім"
    };
    public string Value { get => _value; set => SetProperty(ref _value, value); }
}
