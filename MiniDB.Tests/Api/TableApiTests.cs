using System.Net;
using System.Net.Http.Json;
using System.Text;
using MiniDB.Server.Contracts;
using MiniDB.Server.Persistence;

namespace MiniDB.Tests.Api;

public sealed class TableApiTests : ApiTestBase
{
    [Fact]
    public async Task Create_ReturnsTableLocationAndPersistsSchema()
    {
        await CreateDatabaseAsync();
        using var response = await Client.PostAsJsonAsync(TablesUrl, ValidTable());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TableUrl, response.Headers.Location?.AbsolutePath);
        var table = await ReadAsync<TableResponse>(response);
        Assert.Equal("People", table.Name);
        Assert.Equal(["integer", "string", "real", "char", "color", "colorInvl"],
            table.Columns.Select(column => column.DataTypeName));
        Assert.Empty(table.Rows);
        var stored = await new DatabaseStorage(StoragePath).LoadAsync("Demo");
        Assert.Equal(6, stored.GetTable("People").Columns.Count);
    }

    [Fact]
    public async Task DuplicateTable_ReturnsConflict()
    {
        await CreateTableAsync();
        using var response = await Client.PostAsJsonAsync(TablesUrl, ValidTable());
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Integer")]
    public async Task UnknownType_ReturnsBadRequestWithoutSavingTable(string typeName)
    {
        await CreateDatabaseAsync();
        var request = ValidTable();
        request.Columns[0] = new ColumnRequest { Name = "Id", DataTypeName = typeName };
        using var response = await Client.PostAsJsonAsync(TablesUrl, request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await new DatabaseStorage(StoragePath).LoadAsync("Demo")).Tables);
    }

    [Theory]
    [InlineData("{\"name\":\"\",\"columns\":[{\"name\":\"Id\",\"dataTypeName\":\"integer\"}]}")]
    [InlineData("{\"name\":\"People\",\"columns\":[]}")]
    [InlineData("{\"name\":\"People\",\"columns\":null}")]
    [InlineData("{\"name\":\"People\",\"columns\":[null]}")]
    [InlineData("{\"name\":\"People\",\"columns\":[{\"name\":\"\",\"dataTypeName\":\"integer\"}]}")]
    [InlineData("{\"name\":\"People\",\"columns\":[{\"name\":\"Id\",\"dataTypeName\":\"integer\"},{\"name\":\"Id\",\"dataTypeName\":\"string\"}]}")]
    public async Task InvalidTable_ReturnsBadRequest(string body)
    {
        await CreateDatabaseAsync();
        using var response = await Client.PostAsync(TablesUrl, new StringContent(body, Encoding.UTF8, "application/json"));
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await new DatabaseStorage(StoragePath).LoadAsync("Demo")).Tables);
    }

    [Fact]
    public async Task Get_ReturnsSchemaAndRows()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        var table = await GetTableAsync();
        Assert.Equal("People", table.Name);
        Assert.Equal(6, table.Columns.Count);
        Assert.Equal(Values(), Assert.Single(table.Rows).Values);
    }

    [Fact]
    public async Task Delete_PersistsRemoval()
    {
        await CreateTableAsync();
        using var response = await Client.DeleteAsync(TableUrl);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await new DatabaseStorage(StoragePath).LoadAsync("Demo")).Tables);
        using var missing = await Client.GetAsync(TableUrl);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task MissingTable_ReturnsNotFound(string method)
    {
        await CreateDatabaseAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), TableUrl);
        using var response = await Client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MissingDatabase_ReturnsNotFound()
    {
        using var response = await Client.PostAsJsonAsync(TablesUrl, ValidTable());
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Names_AreCaseSensitive()
    {
        await CreateTableAsync();
        using var create = await Client.PostAsJsonAsync(TablesUrl, ValidTable("people"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var get = await Client.GetAsync(TablesUrl + "/PEOPLE");
        await AssertProblemAsync(get, HttpStatusCode.NotFound);
    }
}
