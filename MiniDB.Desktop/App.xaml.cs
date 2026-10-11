using System.Windows;
using System.IO;
using System.Net.Http;
using MiniDB.Desktop.Services;
using MiniDB.Desktop.ViewModels;
using MiniDB.Desktop.Views;

namespace MiniDB.Desktop;

public partial class App : Application
{
    private HttpClient? _httpClient;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var settings = DesktopSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            _httpClient = new HttpClient { BaseAddress = new Uri(settings.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
            var api = new MiniDbApiClient(_httpClient);
            MainWindow = new MainWindow(new MainViewModel(api));
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show("Не вдалося запустити MiniDB. Перевірте appsettings.json та ApiBaseUrl."
                + Environment.NewLine + exception.Message, "Налаштування MiniDB", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
