using Microsoft.AspNetCore.Mvc;
using MiniDB.Core.Models;
using MiniDB.Server.Contracts;
using MiniDB.Server.Persistence;

namespace MiniDB.Server.Controllers;

[ApiController]
[Route("api/databases")]
public sealed class DatabaseController(DatabaseStorage storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DatabaseResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var databases = await storage.GetAllAsync(cancellationToken);
        return Ok(databases.Select(ApiMapper.ToResponse).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<DatabaseResponse>> Create(CreateDatabaseRequest request,
        CancellationToken cancellationToken)
    {
        if (storage.Exists(request.Name))
        {
            return Conflict(new ProblemDetails
            {
                Status = 409, Title = "База даних вже існує.", Detail = request.Name
            });
        }

        var database = new Database(request.Name);
        await storage.SaveAsync(database, cancellationToken);
        return CreatedAtAction(nameof(Get), new { databaseName = database.Name }, ApiMapper.ToResponse(database));
    }

    [HttpGet("{databaseName}")]
    public async Task<ActionResult<DatabaseResponse>> Get(string databaseName, CancellationToken cancellationToken)
    {
        var database = await storage.LoadAsync(databaseName, cancellationToken);
        return Ok(ApiMapper.ToResponse(database));
    }

    [HttpDelete("{databaseName}")]
    public IActionResult Delete(string databaseName)
    {
        if (!storage.Delete(databaseName))
        {
            throw new KeyNotFoundException($"Базу даних '{databaseName}' не знайдено.");
        }

        return NoContent();
    }
}
