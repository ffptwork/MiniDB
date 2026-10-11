using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MiniDB.Desktop.Models;
using MiniDB.Desktop.ViewModels;

namespace MiniDB.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _model;
    private readonly CancellationTokenSource _shutdown = new();

    public MainWindow(MainViewModel model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 40);
        model.GridColumns.CollectionChanged += (_, _) => RebuildColumns();
        Loaded += async (_, _) => await model.RefreshAsync(_shutdown.Token);
        Closed += (_, _) => { _shutdown.Cancel(); _shutdown.Dispose(); };
    }

    private void RebuildColumns()
    {
        RowsGrid.Columns.Clear();
        foreach (var column in _model.GridColumns)
        {
            var cellStyle = new Style(typeof(TextBlock));
            cellStyle.Setters.Add(new Setter(TextBlock.ToolTipProperty, new Binding(column.BindingPath)));
            cellStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            cellStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(4, 0, 4, 0)));
            RowsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header, Binding = new Binding(column.BindingPath), SortMemberPath = column.BindingPath,
                MinWidth = 110, Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = cellStyle
            });
        }
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await _model.RefreshAsync(_shutdown.Token);
    private async void DatabaseSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_model.IsBusy) await _model.SelectDatabaseAsync(DatabaseList.SelectedItem as DatabaseDto, _shutdown.Token);
    }
    private async void TableSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_model.IsBusy) await _model.SelectTableAsync(TableList.SelectedItem as TableDto, _shutdown.Token);
    }

    private void CreateDatabaseClick(object sender, RoutedEventArgs e) =>
        new CreateDatabaseWindow(_model, _shutdown.Token) { Owner = this }.ShowDialog();
    private void CreateTableClick(object sender, RoutedEventArgs e) =>
        new CreateTableWindow(_model, _shutdown.Token) { Owner = this }.ShowDialog();

    private async void DeleteDatabaseClick(object sender, RoutedEventArgs e)
    {
        if (!_model.CanUseDatabase) return;
        if (MessageBox.Show(this, $"Видалити базу «{_model.SelectedDatabase!.Name}» разом з усіма таблицями?",
                "Видалення бази", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            await _model.DeleteDatabaseAsync(_shutdown.Token);
    }

    private async void DeleteTableClick(object sender, RoutedEventArgs e)
    {
        if (!_model.CanUseTable) return;
        if (MessageBox.Show(this, $"Видалити таблицю «{_model.SelectedTable!.Name}» разом з усіма рядками?",
                "Видалення таблиці", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            await _model.DeleteTableAsync(_shutdown.Token);
    }

    private void AddRowClick(object sender, RoutedEventArgs e)
    {
        if (_model.CanUseTable)
            new RowEditorWindow(_model, null, _shutdown.Token) { Owner = this }.ShowDialog();
    }
    private void EditRowClick(object sender, RoutedEventArgs e)
    {
        if (_model.CanEditRow)
            new RowEditorWindow(_model, _model.SelectedRow!.Row, _shutdown.Token) { Owner = this }.ShowDialog();
    }
    private void RowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(RowsGrid, e.OriginalSource as DependencyObject) is DataGridRow)
            EditRowClick(sender, e);
    }
    private async void SearchClick(object sender, RoutedEventArgs e) => await _model.SearchAsync(_shutdown.Token);
    private async void ResetSearchClick(object sender, RoutedEventArgs e) => await _model.ResetSearchAsync(_shutdown.Token);
    private async void PatternKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _model.CanSearch)
        {
            e.Handled = true;
            await _model.SearchAsync(_shutdown.Token);
        }
    }
}
