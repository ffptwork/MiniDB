using System.Windows;
using System.Windows.Controls;
using MiniDB.Desktop.ViewModels;

namespace MiniDB.Desktop.Views;

public partial class CreateTableWindow : Window
{
    private readonly CreateTableViewModel _model = new();
    private readonly MainViewModel _main;
    private readonly CancellationToken _token;
    private bool _saving;
    public CreateTableWindow(MainViewModel main, CancellationToken token)
    {
        InitializeComponent();
        DataContext = _model;
        _main = main;
        _token = token;
        Loaded += (_, _) => NameBox.Focus();
        Closing += (_, e) => e.Cancel = _saving;
    }
    private void AddColumnClick(object sender, RoutedEventArgs e)
    {
        var column = new ColumnDraft();
        _model.Columns.Add(column);
        ColumnsGrid.SelectedItem = column;
        ColumnsGrid.ScrollIntoView(column);
    }
    private void RemoveColumnClick(object sender, RoutedEventArgs e)
    {
        if (ColumnsGrid.SelectedItem is ColumnDraft column) _model.Columns.Remove(column);
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        ColumnsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ColumnsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _saving = true;
        SetEnabled(false);
        BusyText.Text = "Створення...";
        var created = await _main.CreateTableAsync(_model.BuildRequest(), _token);
        _saving = false;
        if (created) DialogResult = true;
        else { ErrorText.Text = _main.ErrorMessage; BusyText.Text = ""; SetEnabled(true); }
    }
    private void SetEnabled(bool enabled) => SaveButton.IsEnabled = CancelButton.IsEnabled
        = NameBox.IsEnabled = ColumnsGrid.IsEnabled = ColumnButtons.IsEnabled = enabled;
}
