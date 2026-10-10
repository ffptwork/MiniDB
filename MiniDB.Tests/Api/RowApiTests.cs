using System.Net;
using System.Net.Http.Json;
using System.Text;
using MiniDB.Server.Contracts;
using MiniDB.Server.Persistence;

namespace MiniDB.Tests.Api;

public sealed class RowApiTests : ApiTestBase
{
    [Fact]
    public async Task Add_ParsesAllTypesAndPersistsValuesInSchemaOrder()
    {
        await CreateTableAsync();
        using var response = await Client.PostAsJsonAsync(RowsUrl, new AddRowRequest { Values = Values() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var row = await ReadAsync<RowResponse>(response);
        Assert.Equal(0, row.Index);
        Assert.Equal(Values(), row.Values);
        var stored = (await new DatabaseStorage(StoragePath).LoadAsync("Demo")).GetTable("People").Rows[0];
        Assert.IsType<int>(stored.GetValue("Id"));
        Assert.Equal(1.25, Assert.IsType<double>(stored.GetValue("Rating")));
        Assert.Equal('D', Assert.IsType<char>(stored.GetValue("Initial")));
        Assert.IsType<MiniDB.Core.Models.Color>(stored.GetValue("Tint"));
        Assert.IsType<MiniDB.Core.Models.ColorInterval>(stored.GetValue("Range"));
    }

    [Theory]
    [InlineData("Id", "one")]
    [InlineData("Rating", "1,25")]
    [InlineData("Initial", "DD")]
    [InlineData("Tint", "RGB(256, 0, 0)")]
    [InlineData("Range", "[RGB(100, 20, 30); RGB(10, 120, 150)]")]
    public async Task InvalidValue_ReturnsBadRequestWithoutSaving(string column, string value)
    {
        await CreateTableAsync();
        var values = Values();
        values[column] = value;
        using var response = await Client.PostAsJsonAsync(RowsUrl, new AddRowRequest { Values = values });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await GetTableAsync()).Rows);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("wrong-case")]
    public async Task InvalidKeys_ReturnBadRequest(string kind)
    {
        await CreateTableAsync();
        var values = Values();
        if (kind == "extra") values["Unknown"] = "value";
        else values.Remove("Name");
        if (kind == "wrong-case") values["name"] = "Daniel";
        using var response = await Client.PostAsJsonAsync(RowsUrl, new AddRowRequest { Values = values });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await GetTableAsync()).Rows);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"values\":null}")]
    [InlineData("{\"values\":{\"Id\":\"1\",\"Name\":null,\"Rating\":\"1.25\",\"Initial\":\"D\",\"Tint\":\"RGB(255, 0, 0)\",\"Range\":\"[RGB(10, 20, 30); RGB(100, 120, 150)]\"}}")]
    public async Task MissingOrNullValues_ReturnBadRequest(string body)
    {
        await CreateTableAsync();
        using var response = await Client.PostAsync(RowsUrl, new StringContent(body, Encoding.UTF8, "application/json"));
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyAndSpecialStrings_RoundTripThroughHttp()
    {
        await CreateTableAsync();
        await AddRowAsync(Values(""));
        const string text = "Тест \"quotes\" \\ newline\n😀";
        await AddRowAsync(Values(text, "2"));
        var table = await GetTableAsync();
        Assert.Equal("", table.Rows[0].Values["Name"]);
        Assert.Equal(text, table.Rows[1].Values["Name"]);
        Assert.Equal([0, 1], table.Rows.Select(row => row.Index));
    }

    [Fact]
    public async Task Edit_ReplacesValuesAndPersistsChanges()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        using var response = await Client.PutAsJsonAsync(RowsUrl + "/0", new EditRowRequest { Values = Values("Dan", "2") });
        var row = await ReadAsync<RowResponse>(response);
        Assert.Equal(0, row.Index);
        Assert.Equal("Dan", row.Values["Name"]);
        Assert.Equal("2", Assert.Single((await GetTableAsync()).Rows).Values["Id"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(2147483647)]
    public async Task InvalidIndex_ReturnsBadRequest(int index)
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        using var response = await Client.PutAsJsonAsync(RowsUrl + "/" + index, new EditRowRequest { Values = Values("Other") });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Daniel", Assert.Single((await GetTableAsync()).Rows).Values["Name"]);
    }

    [Fact]
    public async Task InvalidEdit_DoesNotPersistPartialChanges()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        var values = Values("Other", "2");
        values["Tint"] = "bad";
        using var response = await Client.PutAsJsonAsync(RowsUrl + "/0", new EditRowRequest { Values = values });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(Values(), Assert.Single((await GetTableAsync()).Rows).Values);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("2147483648")]
    [InlineData("1.2")]
    public async Task NonIntegerIndex_ReturnsBadRequest(string index)
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        using var response = await Client.PutAsJsonAsync(RowsUrl + "/" + index,
            new EditRowRequest { Values = Values("Other") });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Daniel", Assert.Single((await GetTableAsync()).Rows).Values["Name"]);
    }

    [Theory]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    public async Task MissingDatabaseOrTable_ReturnsNotFound(string method, bool databaseExists)
    {
        if (databaseExists) await CreateDatabaseAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "PUT" ? RowsUrl + "/0" : RowsUrl)
        {
            Content = JsonContent.Create(new AddRowRequest { Values = Values() })
        };
        using var response = await Client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentAdds_DoNotLoseRows()
    {
        await CreateTableAsync();
        var responses = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
            Client.PostAsJsonAsync(RowsUrl, new AddRowRequest { Values = Values("Dan", index.ToString()) })));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var table = await GetTableAsync();
            Assert.Equal(8, table.Rows.Count);
            Assert.Equal(8, table.Rows.Select(row => row.Values["Id"]).Distinct().Count());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }
}
