using Microsoft.AspNetCore.Mvc;
using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Core.Validation;
using MiniDB.Server.Contracts;
using MiniDB.Server.Persistence;

namespace MiniDB.Server.Controllers;

[ApiController]
[Route("api/databases/{databaseName}/tables")]
public sealed class TableController(DatabaseStorage storage, TableValidator tableValidator,
    RowValidator rowValidator) : ControllerBase
{
    [HttpGet("{tableName}")]
    public async Task<ActionResult<TableResponse>> Get(string databaseName, string tableName,
        CancellationToken cancellationToken)
    {
        var database = await storage.LoadAsync(databaseName, cancellationToken);
        return Ok(ApiMapper.ToResponse(database.GetTable(tableName)));
    }

    [HttpPost]
    public async Task<ActionResult<TableResponse>> Create(string databaseName, CreateTableRequest request,
        CancellationToken cancellationToken)
    {
        var database = await storage.LoadAsync(databaseName, cancellationToken);
        if (database.Tables.Any(table => table.Name == request.Name))
        {
            return Conflict(new ProblemDetails
            {
                Status = 409, Title = "Таблиця вже існує.", Detail = request.Name
            });
        }

        var table = new Table(request.Name);
        foreach (var column in request.Columns)
        {
            ArgumentNullException.ThrowIfNull(column);
            table.AddColumn(new Column(column.Name, DataTypeFactory.Create(column.DataTypeName)));
        }

        tableValidator.Validate(table);
        database.AddTable(table);
        await storage.SaveAsync(database, cancellationToken);
        return CreatedAtAction(nameof(Get), new { databaseName, tableName = table.Name }, ApiMapper.ToResponse(table));
    }

    [HttpDelete("{tableName}")]
    public async Task<IActionResult> Delete(string databaseName, string tableName,
        CancellationToken cancellationToken)
    {
        var database = await storage.LoadAsync(databaseName, cancellationToken);
        if (!database.RemoveTable(tableName))
        {
            throw new KeyNotFoundException($"Таблицю '{tableName}' не знайдено.");
        }

        await storage.SaveAsync(database, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tableName}/rows")]
    public async Task<ActionResult<RowResponse>> AddRow(string databaseName, string tableName,
        AddRowRequest request, CancellationToken cancellationToken)
    {
        var database = await storage.LoadAsync(databaseName, cancellationToken);
        var table = database.GetTable(tableName);
        var row = table.AddRow(ApiMapper.GetOrderedValues(table, request.Values));
        rowValidator.Validate(table, row);
        await storage.SaveAsync(database, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiMapper.ToResponse(table, row, table.Rows.Count - 1));
    }

    [HttpPut("{tableName}/rows/{rowIndex}")]
    public async Task<ActionResult<RowResponse>> EditRow(string databaseName, string tableName, int rowIndex,
        EditRowRequest request, CancellationToken cancellationToken)
    {
        var database = await storage.LoadAsync(databaseName, cancellationToken);
        var table = database.GetTable(tableName);
        table.EditRow(rowIndex, ApiMapper.GetOrderedValues(table, request.Values));
        var row = table.Rows[rowIndex];
        rowValidator.Validate(table, row);
        await storage.SaveAsync(database, cancellationToken);
        return Ok(ApiMapper.ToResponse(table, row, rowIndex));
    }
}
