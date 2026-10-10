using Microsoft.AspNetCore.Mvc;
using MiniDB.Core.Search;
using MiniDB.Server.Contracts;
using MiniDB.Server.Persistence;

namespace MiniDB.Server.Controllers;

[ApiController]
[Route("api/databases/{databaseName}/tables/{tableName}/search")]
public sealed class SearchController(DatabaseStorage storage, SearchService searchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RowResponse>>> Search(string databaseName, string tableName,
        [FromQuery] SearchRequest request, CancellationToken cancellationToken)
    {
        var database = await storage.LoadAsync(databaseName, cancellationToken);
        var table = database.GetTable(tableName);
        var rows = searchService.Search(table, request.ColumnName, request.Pattern);
        var indices = table.Rows.Select((row, index) => (row, index))
            .ToDictionary(item => item.row, item => item.index);
        return Ok(rows.Select(row => ApiMapper.ToResponse(table, row, indices[row])).ToArray());
    }
}
