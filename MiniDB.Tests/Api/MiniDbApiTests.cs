using System.Net;
using System.Net.Http.Json;
using MiniDB.Server.Contracts;
using MiniDB.Server.Persistence;

namespace MiniDB.Tests.Api;

public sealed class MiniDbApiTests : ApiTestBase
{
    [Fact]
    public async Task Health_ReturnsOkWithoutRedirect()
    {
        using var response = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ok", (await ReadAsync<Dictionary<string, string>>(response))["status"]);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task CreateDatabaseAndTable_PersistsThroughRest()
    {
        await CreateTableAsync();
        using var response = await Client.GetAsync("/api/databases/Demo");
        var database = await ReadAsync<DatabaseResponse>(response);
        Assert.Equal("Demo", database.Name);
        Assert.Equal(6, Assert.Single(database.Tables).Columns.Count);
        Assert.Single((await new DatabaseStorage(StoragePath).LoadAsync("Demo")).Tables);
    }

    [Fact]
    public async Task SearchRest_ReturnsMatchingRowsWithOriginalIndex()
    {
        await CreateTableAsync();
        await AddRowAsync(Values("Anna", "1"));
        await AddRowAsync(Values("Daniel", "2"));
        using var response = await Client.GetAsync(TableUrl + "/search?columnName=Name&pattern=Dan%2A");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(await ReadAsync<RowResponse[]>(response));
        Assert.Equal(1, row.Index);
        Assert.Equal("Daniel", row.Values["Name"]);
    }
}
