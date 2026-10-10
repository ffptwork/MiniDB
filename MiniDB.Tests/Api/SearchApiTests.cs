using System.Net;
using MiniDB.Server.Contracts;

namespace MiniDB.Tests.Api;

public sealed class SearchApiTests : ApiTestBase
{
    [Theory]
    [InlineData("Name", "Daniel", "1")]
    [InlineData("Name", "Dan*", "1,2")]
    [InlineData("Name", "*test*", "3")]
    [InlineData("Name", "*", "1,2,3,4")]
    [InlineData("Name", "", "4")]
    [InlineData("Name", "missing", "")]
    [InlineData("Name", "dan*", "")]
    [InlineData("Id", "0002", "2")]
    [InlineData("Rating", "1.2500", "1,2,3,4")]
    [InlineData("Initial", "D*", "1,2,3,4")]
    [InlineData("Tint", "RGB(255,0,0)", "1,2,3,4")]
    [InlineData("Range", "[RGB(10,20,30);RGB(100,120,150)]", "1,2,3,4")]
    public async Task Search_UsesCoreMatchingAndReturnsOriginalIndices(string column, string pattern, string expectedIds)
    {
        await CreateTableAsync();
        await AddRowAsync(Values("Daniel", "1"));
        await AddRowAsync(Values("Dan", "2"));
        await AddRowAsync(Values("a test message", "3"));
        await AddRowAsync(Values("", "4"));
        using var response = await Client.GetAsync(SearchUrl(column, pattern));
        var rows = await ReadAsync<RowResponse[]>(response);
        Assert.Equal(expectedIds.Length == 0 ? [] : expectedIds.Split(','), rows.Select(row => row.Values["Id"]));
        Assert.All(rows, row => Assert.Equal(int.Parse(row.Values["Id"]) - 1, row.Index));
        Assert.Equal(4, (await GetTableAsync()).Rows.Count);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("name")]
    public async Task MissingColumn_ReturnsNotFound(string column)
    {
        await CreateTableAsync();
        using var response = await Client.GetAsync(SearchUrl(column, "*"));
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Id", "*")]
    [InlineData("Rating", "1,25")]
    [InlineData("Tint", "RGB(256, 0, 0)")]
    [InlineData("Range", "bad")]
    public async Task InvalidTypedPattern_ReturnsBadRequestEvenForEmptyTable(string column, string pattern)
    {
        await CreateTableAsync();
        using var response = await Client.GetAsync(SearchUrl(column, pattern));
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("?columnName=Name")]
    [InlineData("?pattern=*")]
    [InlineData("?columnName=&pattern=*")]
    public async Task MissingQueryParameters_ReturnBadRequest(string query)
    {
        await CreateTableAsync();
        using var response = await Client.GetAsync(TableUrl + "/search" + query);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MissingTable_ReturnsNotFound()
    {
        await CreateDatabaseAsync();
        using var response = await Client.GetAsync(SearchUrl("Name", "*"));
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MissingDatabase_ReturnsNotFound()
    {
        using var response = await Client.GetAsync(SearchUrl("Name", "*"));
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    private static string SearchUrl(string column, string pattern) =>
        TableUrl + "/search?columnName=" + Uri.EscapeDataString(column) + "&pattern=" + Uri.EscapeDataString(pattern);
}
