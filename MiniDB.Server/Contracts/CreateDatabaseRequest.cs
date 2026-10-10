using System.ComponentModel.DataAnnotations;

namespace MiniDB.Server.Contracts;

public sealed class CreateDatabaseRequest
{
    [Required]
    public required string Name { get; init; }
}
