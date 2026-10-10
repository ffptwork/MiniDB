using System.Net;
using System.Net.Http.Json;
using System.Text;
using MiniDB.Server.Contracts;
using MiniDB.Server.Persistence;

namespace MiniDB.Tests.Api;

public sealed class DatabaseApiTests : ApiTestBase
{
    [Fact]
    public async Task Create_ReturnsCreatedLocationAndPersistsEmptyDatabase()
    {
        using var response = await Client.PostAsJsonAsync("/api/databases", new CreateDatabaseRequest { Name = "Demo" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/databases/Demo", response.Headers.Location?.AbsolutePath);
        var database = await ReadAsync<DatabaseResponse>(response);
        Assert.Equal("Demo", database.Name);
        Assert.Empty(database.Tables);
        Assert.Equal("Demo", (await new DatabaseStorage(StoragePath).LoadAsync("Demo")).Name);
    }

    [Fact]
    public async Task Create_DuplicateReturnsConflict()
    {
        await CreateDatabaseAsync();
        using var response = await Client.PostAsJsonAsync("/api/databases", new CreateDatabaseRequest { Name = "Demo" });
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_ReturnsCompleteDatabase()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        using var response = await Client.GetAsync("/api/databases/Demo");
        var database = await ReadAsync<DatabaseResponse>(response);
        var table = Assert.Single(database.Tables);
        Assert.Equal("People", table.Name);
        Assert.Equal(6, table.Columns.Count);
        Assert.Equal("Daniel", Assert.Single(table.Rows).Values["Name"]);
    }

    [Fact]
    public async Task List_ReturnsAvailableDatabases()
    {
        await CreateDatabaseAsync("Beta");
        await CreateDatabaseAsync("Alpha");
        using var response = await Client.GetAsync("/api/databases");
        var databases = await ReadAsync<DatabaseResponse[]>(response);
        Assert.Equal(["Alpha", "Beta"], databases.Select(database => database.Name));
    }

    [Fact]
    public async Task List_EmptyStorageReturnsEmptyArray()
    {
        using var response = await Client.GetAsync("/api/databases");
        Assert.Empty(await ReadAsync<DatabaseResponse[]>(response));
    }

    [Fact]
    public async Task Delete_RemovesPersistedDatabase()
    {
        await CreateDatabaseAsync();
        using var response = await Client.DeleteAsync("/api/databases/Demo");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(new DatabaseStorage(StoragePath).Exists("Demo"));
        using var missing = await Client.GetAsync("/api/databases/Demo");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task MissingDatabase_ReturnsNotFound(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/databases/Missing");
        using var response = await Client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../escape")]
    [InlineData("C:\\escape")]
    [InlineData("bad/name")]
    public async Task InvalidName_ReturnsBadRequestWithoutWritingFiles(string name)
    {
        using var response = await Client.PostAsJsonAsync("/api/databases", new CreateDatabaseRequest { Name = name });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(Directory.GetFiles(StoragePath, "*.json"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":null}")]
    [InlineData("{broken")]
    public async Task InvalidBody_ReturnsBadRequest(string body)
    {
        using var response = await Client.PostAsync("/api/databases", new StringContent(body, Encoding.UTF8, "application/json"));
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CorruptStorage_ReturnsSafeServerError()
    {
        await CreateDatabaseAsync();
        await File.WriteAllTextAsync(Path.Combine(StoragePath, "Demo.json"), "{broken");
        using var response = await Client.GetAsync("/api/databases/Demo");
        await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(StoragePath, body);
        Assert.DoesNotContain("InvalidDataException", body);
    }

    [Fact]
    public async Task ConcurrentCreates_ProduceOneCreatedAndOneConflict()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            Client.PostAsJsonAsync("/api/databases", new CreateDatabaseRequest { Name = "Demo" })));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }
}
