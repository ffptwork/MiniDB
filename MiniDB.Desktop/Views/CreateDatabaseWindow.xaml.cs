using System.Windows;
using MiniDB.Desktop.ViewModels;

namespace MiniDB.Desktop.Views;

public partial class CreateDatabaseWindow : Window
{
    private readonly MainViewModel _main;
    private readonly CancellationToken _token;
    private bool _saving;
    public CreateDatabaseWindow(MainViewModel main, CancellationToken token)
    {
        InitializeComponent();
        _main = main;
        _token = token;
        Loaded += (_, _) => NameBox.Focus();
        Closing += (_, e) => e.Cancel = _saving;
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        _saving = true;
        SaveButton.IsEnabled = CancelButton.IsEnabled = NameBox.IsEnabled = false;
        BusyText.Text = "Створення...";
        var created = await _main.CreateDatabaseAsync(NameBox.Text, _token);
        _saving = false;
        if (created) DialogResult = true;
        else
        {
            ErrorText.Text = _main.ErrorMessage;
            BusyText.Text = "";
            SaveButton.IsEnabled = CancelButton.IsEnabled = NameBox.IsEnabled = true;
        }
    }
}
