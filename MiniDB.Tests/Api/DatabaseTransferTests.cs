using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MiniDB.Server.Contracts;

namespace MiniDB.Tests.Api;

public sealed class DatabaseTransferTests : ApiTestBase
{
    private async Task<HttpResponseMessage> ImportAsync(byte[] json)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(json), "file", "Demo.json");
        return await Client.PostAsync("/api/databases/import", form);
    }

    [Fact]
    public async Task ExportImport_RestoresCanonicalSchemaValuesAndSearch()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        await AddRowAsync(Values("Danylo", "2"));
        using var export = await Client.GetAsync("/api/databases/Demo/export");
        Assert.Equal("application/json", export.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Demo.json", export.Content.Headers.ContentDisposition?.ToString());
        var json = await export.Content.ReadAsByteArrayAsync();
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(StoragePath, "Demo.json")), json);
        using var deleted = await Client.DeleteAsync("/api/databases/Demo");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var imported = await ImportAsync(json);
        Assert.Equal(HttpStatusCode.Created, imported.StatusCode);
        var table = Assert.Single((await ReadAsync<DatabaseResponse>(imported)).Tables);
        Assert.Equal(6, table.Columns.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(Values(), table.Rows[0].Values);
        using var search = await Client.GetAsync(TableUrl + "/search?columnName=Tint&pattern=RGB%28255%2C0%2C0%29");
        Assert.Equal(2, (await ReadAsync<RowResponse[]>(search)).Length);
    }

    [Fact]
    public async Task Import_RejectsMalformedOrInvalidDatabaseWithoutSaving()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        using var export = await Client.GetAsync("/api/databases/Demo/export");
        var valid = await export.Content.ReadAsStringAsync();
        using var deleted = await Client.DeleteAsync("/api/databases/Demo");
        var invalid = new List<string> { "", "{broken", "{}", "null" };
        void Changed(Action<JsonNode> change)
        {
            var document = JsonNode.Parse(valid)!;
            change(document);
            invalid.Add(document.ToJsonString());
        }
        Changed(node => node["Tables"]![0]!["Columns"]![0]!["DataTypeName"] = "unknown");
        Changed(node => node["Tables"]![0]!["Rows"]![0]!["Values"]!["Tint"] = "RGB(256, 0, 0)");
        Changed(node => node["Tables"]![0]!["Rows"]![0]!["Values"]!["Range"] = "[RGB(100, 0, 0); RGB(10, 0, 0)]");
        Changed(node => node["Tables"]![0]!["Rows"]![0]!["Values"]!.AsObject().Remove("Id"));
        Changed(node => node["Name"] = "../escape");
        foreach (var body in invalid)
        {
            using var response = await ImportAsync(Encoding.UTF8.GetBytes(body));
            await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        }
        Assert.Empty(Directory.GetFiles(StoragePath, "*.json"));
    }

    [Fact]
    public async Task Import_ExistingDatabaseReturnsConflictWithoutOverwriting()
    {
        await CreateTableAsync();
        await AddRowAsync(Values());
        using var export = await Client.GetAsync("/api/databases/Demo/export");
        var json = await export.Content.ReadAsByteArrayAsync();
        using var imported = await ImportAsync(json);
        await AssertProblemAsync(imported, HttpStatusCode.Conflict);
        Assert.Equal(json, await File.ReadAllBytesAsync(Path.Combine(StoragePath, "Demo.json")));
    }
}
