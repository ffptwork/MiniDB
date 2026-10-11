using System.Collections.ObjectModel;
using System.IO;
using MiniDB.Desktop.Models;
using MiniDB.Desktop.Services;

namespace MiniDB.Desktop.ViewModels;

public sealed class MainViewModel(MiniDbApiClient api) : ObservableObject
{
    private DatabaseDto? _selectedDatabase;
    private TableDto? _selectedTable;
    private TableDto? _currentTable;
    private RowDisplayItem? _selectedRow;
    private ColumnDto? _searchColumn;
    private string _pattern = "";
    private string _errorMessage = "";
    private bool _isBusy;
    private bool _isSearchActive;

    public ObservableCollection<DatabaseDto> Databases { get; } = [];
    public ObservableCollection<TableDto> Tables { get; } = [];
    public ObservableCollection<ColumnDto> Columns { get; } = [];
    public ObservableCollection<GridColumnDefinition> GridColumns { get; } = [];
    public ObservableCollection<RowDisplayItem> Rows { get; } = [];
    public DatabaseDto? SelectedDatabase => _selectedDatabase;
    public TableDto? SelectedTable => _selectedTable;
    public TableDto? CurrentTable => _currentTable;
    public bool IsBusy => _isBusy;
    public bool CanInteract => !IsBusy;
    public bool CanUseDatabase => !IsBusy && SelectedDatabase is not null;
    public bool CanUseTable => !IsBusy && CurrentTable is not null;
    public bool CanEditRow => CanUseTable && SelectedRow is not null;
    public bool CanSearch => CanUseTable && SearchColumn is not null;
    public bool IsSearchActive => _isSearchActive;
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string DatabaseTitle => SelectedDatabase?.Name ?? "Виберіть базу даних";
    public string TableTitle => CurrentTable?.Name ?? "Виберіть або створіть таблицю";
    public string SchemaSummary => string.Join("   •   ", Columns.Select(column => $"{column.Name} : {column.DataTypeName}"));
    public string Status => IsBusy ? "Завантаження..." : CurrentTable is null
        ? "Виберіть базу й таблицю для роботи."
        : $"Показано {Rows.Count} із {CurrentTable.Rows.Count} рядків{(IsSearchActive ? " • результати пошуку" : "")}. Зміни зберігаються автоматично.";
    public string SearchHint => SearchColumn is null ? "Виберіть колонку для пошуку." : SearchColumn.DataTypeName is "string" or "char"
        ? "* — будь-яка послідовність символів. Приклади: Dan*, *test*, *"
        : "Для цього типу введіть точне значення. real: 1.25; color: RGB(255, 0, 0).";

    public RowDisplayItem? SelectedRow
    {
        get => _selectedRow;
        set { if (SetProperty(ref _selectedRow, value)) OnPropertyChanged(nameof(CanEditRow)); }
    }

    public ColumnDto? SearchColumn
    {
        get => _searchColumn;
        set
        {
            if (!SetProperty(ref _searchColumn, value)) return;
            OnPropertyChanged(nameof(CanSearch));
            OnPropertyChanged(nameof(SearchHint));
        }
    }

    public string Pattern { get => _pattern; set => SetProperty(ref _pattern, value); }

    public Task<bool> RefreshAsync(CancellationToken token = default) =>
        RunAsync(() => RefreshCoreAsync(SelectedDatabase?.Name, SelectedTable?.Name, token), token);

    public Task<bool> SelectDatabaseAsync(DatabaseDto? database, CancellationToken token = default) =>
        RunAsync(() => SelectDatabaseCoreAsync(database, null, token), token);

    public Task<bool> SelectTableAsync(TableDto? table, CancellationToken token = default) =>
        RunAsync(() => LoadTableCoreAsync(table, token), token);

    public Task<bool> CreateDatabaseAsync(string name, CancellationToken token = default) =>
        ChangeAsync(() => api.CreateDatabaseAsync(name, token), () => RefreshCoreAsync(name, null, token), token);

    public Task<bool> ExportDatabaseAsync(string filePath, CancellationToken token = default)
    {
        if (SelectedDatabase is not { } database) return Task.FromResult(false);
        return RunAsync(async () =>
        {
            var json = await api.ExportDatabaseAsync(database.Name, token);
            await File.WriteAllBytesAsync(filePath, json, token);
        }, token);
    }

    public Task<bool> ImportDatabaseAsync(string filePath, CancellationToken token = default)
    {
        DatabaseDto? imported = null;
        return ChangeAsync(async () =>
        {
            var json = await File.ReadAllBytesAsync(filePath, token);
            imported = await api.ImportDatabaseAsync(json, Path.GetFileName(filePath), token);
        }, () => RefreshCoreAsync(imported!.Name, null, token), token);
    }

    public Task<bool> DeleteDatabaseAsync(CancellationToken token = default)
    {
        if (SelectedDatabase is not { } database) return Task.FromResult(false);
        return ChangeAsync(() => api.DeleteDatabaseAsync(database.Name, token), () => RefreshCoreAsync(null, null, token), token);
    }

    public Task<bool> CreateTableAsync(CreateTableRequest request, CancellationToken token = default)
    {
        if (SelectedDatabase is not { } database) return Task.FromResult(false);
        return ChangeAsync(() => api.CreateTableAsync(database.Name, request, token),
            () => RefreshCoreAsync(database.Name, request.Name, token), token);
    }

    public Task<bool> DeleteTableAsync(CancellationToken token = default)
    {
        if (SelectedDatabase is not { } database || SelectedTable is not { } table) return Task.FromResult(false);
        return ChangeAsync(() => api.DeleteTableAsync(database.Name, table.Name, token),
            () => RefreshCoreAsync(database.Name, null, token), token);
    }

    public Task<bool> AddRowAsync(Dictionary<string, string> values, CancellationToken token = default)
    {
        if (SelectedDatabase is not { } database || SelectedTable is not { } table) return Task.FromResult(false);
        return ChangeAsync(() => api.AddRowAsync(database.Name, table.Name, values, token),
            () => LoadTableCoreAsync(table, token), token);
    }

    public Task<bool> EditRowAsync(int rowIndex, Dictionary<string, string> values, CancellationToken token = default)
    {
        if (SelectedDatabase is not { } database || SelectedTable is not { } table) return Task.FromResult(false);
        return ChangeAsync(() => api.EditRowAsync(database.Name, table.Name, rowIndex, values, token),
            () => LoadTableCoreAsync(table, token), token);
    }

    public Task<bool> SearchAsync(CancellationToken token = default)
    {
        if (SelectedDatabase is not { } database || SelectedTable is not { } table || SearchColumn is not { } column)
            return Task.FromResult(false);
        var pattern = Pattern;
        return RunAsync(async () =>
        {
            var rows = await api.SearchAsync(database.Name, table.Name, column.Name, pattern, token);
            Replace(Rows, TableGridBuilder.BuildRows(Columns, rows));
            SelectedRow = null;
            _isSearchActive = true;
            OnPropertyChanged(nameof(IsSearchActive));
            OnPropertyChanged(nameof(Status));
        }, token);
    }

    public Task<bool> ResetSearchAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        await LoadTableCoreAsync(SelectedTable, token);
        Pattern = "";
    }, token);

    private async Task RefreshCoreAsync(string? databaseName, string? tableName, CancellationToken token)
    {
        var databases = await api.GetDatabasesAsync(token);
        Replace(Databases, databases);
        var selected = Databases.FirstOrDefault(database => database.Name == databaseName) ?? Databases.FirstOrDefault();
        await SelectDatabaseCoreAsync(selected, tableName, token);
    }

    private async Task SelectDatabaseCoreAsync(DatabaseDto? database, string? tableName, CancellationToken token)
    {
        _selectedDatabase = database;
        OnPropertyChanged(nameof(SelectedDatabase));
        OnPropertyChanged(nameof(DatabaseTitle));
        Tables.Clear();
        ClearTable();
        if (database is null) return;
        var loaded = await api.GetDatabaseAsync(database.Name, token);
        Replace(Tables, loaded.Tables);
        await LoadTableCoreAsync(Tables.FirstOrDefault(table => table.Name == tableName) ?? Tables.FirstOrDefault(), token);
    }

    private async Task LoadTableCoreAsync(TableDto? table, CancellationToken token)
    {
        var searchColumnName = SearchColumn?.Name;
        ClearTable();
        _selectedTable = table;
        OnPropertyChanged(nameof(SelectedTable));
        if (table is null || SelectedDatabase is null) return;
        _currentTable = await api.GetTableAsync(SelectedDatabase.Name, table.Name, token);
        Replace(Columns, CurrentTable!.Columns);
        Replace(GridColumns, TableGridBuilder.BuildColumns(Columns));
        Replace(Rows, TableGridBuilder.BuildRows(Columns, CurrentTable.Rows));
        SearchColumn = Columns.FirstOrDefault(column => column.Name == searchColumnName) ?? Columns.FirstOrDefault();
        NotifyTable();
    }

    private void ClearTable()
    {
        _selectedTable = null;
        _currentTable = null;
        _isSearchActive = false;
        SelectedRow = null;
        SearchColumn = null;
        Columns.Clear();
        GridColumns.Clear();
        Rows.Clear();
        OnPropertyChanged(nameof(SelectedTable));
        NotifyTable();
    }

    private void NotifyTable()
    {
        foreach (var name in new[] { nameof(CurrentTable), nameof(TableTitle), nameof(SchemaSummary), nameof(IsSearchActive), nameof(Status) })
            OnPropertyChanged(name);
        NotifyActions();
    }

    private void NotifyActions()
    {
        foreach (var name in new[] { nameof(CanInteract), nameof(CanUseDatabase), nameof(CanUseTable), nameof(CanEditRow), nameof(CanSearch) })
            OnPropertyChanged(name);
    }

    private async Task<bool> ChangeAsync(Func<Task> change, Func<Task> refresh, CancellationToken token)
    {
        var committed = false;
        var completed = await RunAsync(async () =>
        {
            await change();
            committed = true;
            await refresh();
        }, token);
        if (committed && !completed && !token.IsCancellationRequested)
            ErrorMessage = "Зміни збережено, але не вдалося оновити дані. Натисніть «Оновити»." + Environment.NewLine + ErrorMessage;
        return committed;
    }

    private async Task<bool> RunAsync(Func<Task> action, CancellationToken token)
    {
        if (IsBusy) return false;
        _isBusy = true;
        ErrorMessage = "";
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(Status));
        NotifyActions();
        try
        {
            token.ThrowIfCancellationRequested();
            await action();
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        catch (Exception exception)
        {
            ErrorMessage = UserErrorFormatter.Format(exception);
            return false;
        }
        finally
        {
            _isBusy = false;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(Status));
            NotifyActions();
        }
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values) collection.Add(value);
    }
}
