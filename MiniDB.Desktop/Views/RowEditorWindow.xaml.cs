using System.Windows;
using MiniDB.Desktop.Models;
using MiniDB.Desktop.ViewModels;

namespace MiniDB.Desktop.Views;

public partial class RowEditorWindow : Window
{
    private readonly MainViewModel _main;
    private readonly RowEditorViewModel _model;
    private readonly RowDto? _row;
    private readonly CancellationToken _token;
    private bool _saving;
    public RowEditorWindow(MainViewModel main, RowDto? row, CancellationToken token)
    {
        InitializeComponent();
        _main = main;
        _row = row;
        _token = token;
        _model = new RowEditorViewModel(main.Columns, row);
        DataContext = _model;
        Title = Heading.Text = row is null ? "Додати рядок" : $"Редагувати рядок #{row.Index}";
        SaveButton.Content = row is null ? "Додати" : "Застосувати";
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 50);
        Closing += (_, e) => e.Cancel = _saving;
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        _saving = true;
        SetEnabled(false);
        BusyText.Text = "Надсилання...";
        var saved = _row is null
            ? await _main.AddRowAsync(_model.BuildValues(), _token)
            : await _main.EditRowAsync(_row.Index, _model.BuildValues(), _token);
        _saving = false;
        if (saved) DialogResult = true;
        else { ErrorText.Text = _main.ErrorMessage; BusyText.Text = ""; SetEnabled(true); }
    }
    private void SetEnabled(bool enabled) => SaveButton.IsEnabled = CancelButton.IsEnabled = FieldsControl.IsEnabled = enabled;
}
