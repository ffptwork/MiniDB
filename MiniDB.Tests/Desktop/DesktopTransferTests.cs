using MiniDB.Desktop.Services;
using MiniDB.Desktop.ViewModels;
using MiniDB.Tests.Api;

namespace MiniDB.Tests.Desktop;

public sealed class DesktopTransferTests : ApiTestBase
{
    [Fact]
    public async Task Desktop_TransfersLocalFileAndSelectsImportedDatabase()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        var model = new MainViewModel(new MiniDbApiClient(Client));
        Assert.True(await model.RefreshAsync());
        var file = Path.Combine(StoragePath, "transfer.tmp");
        Assert.True(await model.ExportDatabaseAsync(file));
        Assert.True(File.Exists(file));
        Assert.True(await model.DeleteDatabaseAsync());
        Assert.True(await model.ImportDatabaseAsync(file));
        Assert.Equal("Demo", model.SelectedDatabase?.Name);
        Assert.Equal(6, model.Columns.Count);
        Assert.Equal("Daniel", Assert.Single(model.Rows).Row.Values["Name"]);
        Assert.False(await model.ImportDatabaseAsync(file));
        Assert.Contains("вже існує", model.ErrorMessage);
        Assert.True(model.CanInteract);
    }
}
