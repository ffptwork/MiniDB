using System.Net;
using System.Net.Http.Json;
using MiniDB.Tests.Api;
using MiniDB.Web.Models;
using MiniDB.Web.Services;

namespace MiniDB.Tests.Web;

public sealed class WebWorkspaceTests : ApiTestBase
{
    [Fact]
    public async Task Workspace_SearchEditAndTransfer_RestoresSchemaAndRows()
    {
        var api = new MiniDbApiClient(Client);
        var state = new DatabaseWorkspace(api);
        Assert.True(await state.RefreshAsync());
        Assert.True(await state.CreateDatabaseAsync("WebDemo"));
        Assert.Equal("WebDemo", state.Database!.Name);
        var columns = new ColumnRequest[]
        {
            new("Id", "integer"), new("Name", "string"), new("Grade", "real"),
            new("Letter", "char"), new("FavoriteColor", "color"), new("AllowedColors", "colorInvl")
        };
        Assert.True(await state.CreateTableAsync(new("People", columns)));
        Assert.True(await state.SaveRowAsync(null, RowValues("1", "Anna", "RGB(0, 0, 255)")));
        Assert.True(await state.SaveRowAsync(null, RowValues("2", "Daniel", "RGB(255, 0, 0)")));
        state.ColumnName = "Name";
        state.Pattern = "Dan*";
        Assert.True(await state.SearchAsync());
        var result = Assert.Single(state.Rows);
        Assert.Equal(1, result.Index);
        Assert.True(await state.SaveRowAsync(result, RowValues("2", "Danylo", "RGB(255, 0, 0)")));
        Assert.Equal("Anna", state.Rows[0].Values["Name"]);
        Assert.Equal("Danylo", state.Rows[1].Values["Name"]);
        state.ColumnName = "FavoriteColor";
        state.Pattern = "RGB(255,0,0)";
        Assert.True(await state.SearchAsync());
        Assert.Equal(1, Assert.Single(state.Rows).Index);
        Assert.True(await state.ResetSearchAsync());
        Assert.False(state.IsFiltered);
        Assert.Equal(2, state.Rows.Count);
        var expected = state.Table!;
        var json = await api.ExportDatabaseAsync("WebDemo");
        Assert.True(await state.DeleteDatabaseAsync());
        using var file = new MemoryStream(json);
        Assert.True(await state.ImportAsync(file, "WebDemo.json"));
        Assert.Equal("WebDemo", state.Database!.Name);
        Assert.True(await state.SelectTableAsync("People"));
        Assert.Equal(expected.Columns, state.Table!.Columns);
        Assert.Equal(expected.Rows.Count, state.Rows.Count);
        foreach (var row in expected.Rows)
            Assert.Equal(row.Values.OrderBy(pair => pair.Key), state.Rows[row.Index].Values.OrderBy(pair => pair.Key));
        Assert.Null(state.Error);
    }

    [Fact]
    public async Task SuccessfulSave_WithFailedRefresh_RemainsCommitted()
    {
        var postCount = 0;
        using var handler = new ResponseHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                postCount++;
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(new DatabaseDto("Demo", [])) };
            }
            return new(HttpStatusCode.ServiceUnavailable)
            {
                Content = JsonContent.Create(new ProblemDetailsDto { Title = "Сервер тимчасово недоступний." })
            };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://minidb.example/") };
        var state = new DatabaseWorkspace(new MiniDbApiClient(http));
        Assert.True(await state.CreateDatabaseAsync("Demo"));
        Assert.Equal(1, postCount);
        Assert.StartsWith("Зміни збережено", state.Error);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, state.ApiError!.StatusCode);
        Assert.False(state.IsBusy);
    }

    private static Dictionary<string, string> RowValues(string id, string name, string color) => new()
    {
        ["Id"] = id, ["Name"] = name, ["Grade"] = "4.5", ["Letter"] = "D",
        ["FavoriteColor"] = color, ["AllowedColors"] = "[RGB(10, 20, 30); RGB(100, 120, 150)]"
    };

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
