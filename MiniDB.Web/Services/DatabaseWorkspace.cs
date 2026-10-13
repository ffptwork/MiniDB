using System.Text.Json;
using MiniDB.Web.Models;

namespace MiniDB.Web.Services;

public sealed class DatabaseWorkspace(MiniDbApiClient api)
{
    public IReadOnlyList<DatabaseDto> Databases { get; private set; } = [];
    public DatabaseDto? Database { get; private set; }
    public TableDto? Table { get; private set; }
    public IReadOnlyList<RowDto> Rows { get; private set; } = [];
    public string ColumnName { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public bool IsFiltered { get; private set; }
    public bool IsBusy { get; private set; }
    public string? Error { get; private set; }
    public ApiException? ApiError { get; private set; }

    public void ClearError() { Error = null; ApiError = null; }
    public void SetError(string message) { ClearError(); Error = message; }

    public async Task<bool> RunAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return false;
        IsBusy = true;
        ClearError();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        catch (ApiException exception) { ApiError = exception; Error = exception.Title; }
        catch (HttpRequestException) { Error = "Не вдалося підключитися до сервера."; }
        catch (OperationCanceledException) { Error = "Сервер не відповів вчасно. Спробуйте ще раз."; }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            Error = exception is InvalidDataException or JsonException
                ? "Сервер повернув некоректну відповідь." : "Не вдалося прочитати файл.";
        }
        finally { IsBusy = false; }
        return false;
    }

    public Task<bool> RefreshAsync(CancellationToken token = default) =>
        RunAsync(() => RefreshCoreAsync(Database?.Name, Table?.Name, token), token);

    public Task<bool> SelectDatabaseAsync(string name, CancellationToken token = default) =>
        RunAsync(async () =>
        {
            var database = await api.GetDatabaseAsync(name, token);
            Database = database;
            SetTable(null);
        }, token);

    public Task<bool> SelectTableAsync(string name, CancellationToken token = default) =>
        RunAsync(async () => SetTable(await api.GetTableAsync(Database!.Name, name, token)), token);

    public Task<bool> CreateDatabaseAsync(string name, CancellationToken token = default) =>
        SaveAndRefreshAsync(async () => { await api.CreateDatabaseAsync(name, token); },
            () => RefreshCoreAsync(name, null, token), token);

    public Task<bool> DeleteDatabaseAsync(CancellationToken token = default) =>
        SaveAndRefreshAsync(() => api.DeleteDatabaseAsync(Database!.Name, token),
            () => RefreshCoreAsync(null, null, token), token);

    public Task<bool> CreateTableAsync(CreateTableRequest request, CancellationToken token = default) =>
        SaveAndRefreshAsync(async () => { await api.CreateTableAsync(Database!.Name, request, token); },
            () => RefreshCoreAsync(Database!.Name, request.Name, token), token);

    public Task<bool> DeleteTableAsync(CancellationToken token = default) =>
        SaveAndRefreshAsync(() => api.DeleteTableAsync(Database!.Name, Table!.Name, token),
            () => RefreshCoreAsync(Database!.Name, null, token), token);

    public Task<bool> SaveRowAsync(RowDto? row, Dictionary<string, string> values, CancellationToken token = default) =>
        SaveAndRefreshAsync(async () =>
        {
            if (row is null) await api.AddRowAsync(Database!.Name, Table!.Name, values, token);
            else await api.EditRowAsync(Database!.Name, Table!.Name, row.Index, values, token);
        }, () => RefreshTableAsync(token), token);

    public Task<bool> SearchAsync(CancellationToken token = default) =>
        RunAsync(async () =>
        {
            var rows = await api.SearchAsync(Database!.Name, Table!.Name, ColumnName, Pattern, token);
            Rows = rows;
            IsFiltered = true;
        }, token);

    public Task<bool> ResetSearchAsync(CancellationToken token = default) =>
        RunAsync(() => RefreshTableAsync(token), token);

    public Task<bool> ImportAsync(Stream file, string fileName, CancellationToken token = default)
    {
        string? name = null;
        return SaveAndRefreshAsync(async () => { name = (await api.ImportDatabaseAsync(file, fileName, token)).Name; },
            () => RefreshCoreAsync(name, null, token), token);
    }

    private async Task<bool> SaveAndRefreshAsync(Func<Task> save, Func<Task> refresh, CancellationToken token)
    {
        var committed = false;
        await RunAsync(async () =>
        {
            await save();
            committed = true;
            await refresh();
        }, token);
        if (committed && Error is not null)
            Error = "Зміни збережено, але не вдалося оновити дані. " + Error;
        return committed;
    }

    private async Task RefreshCoreAsync(string? databaseName, string? tableName, CancellationToken token)
    {
        var databases = await api.GetDatabasesAsync(token);
        var database = databases.FirstOrDefault(database => database.Name == databaseName);
        if (database is not null) database = await api.GetDatabaseAsync(database.Name, token);
        var table = database?.Tables.FirstOrDefault(table => table.Name == tableName);
        if (table is not null) table = await api.GetTableAsync(database!.Name, table.Name, token);
        Databases = databases;
        Database = database;
        SetTable(table);
    }

    private async Task RefreshTableAsync(CancellationToken token) =>
        SetTable(await api.GetTableAsync(Database!.Name, Table!.Name, token));

    private void SetTable(TableDto? table)
    {
        Table = table;
        Rows = table?.Rows ?? [];
        ColumnName = table?.Columns.FirstOrDefault()?.Name ?? string.Empty;
        Pattern = string.Empty;
        IsFiltered = false;
    }
}
