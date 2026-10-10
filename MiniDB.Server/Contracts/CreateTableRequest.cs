using System.ComponentModel.DataAnnotations;

namespace MiniDB.Server.Contracts;

public sealed class CreateTableRequest
{
    [Required]
    public required string Name { get; init; }

    [Required]
    public required List<ColumnRequest> Columns { get; init; }
}
