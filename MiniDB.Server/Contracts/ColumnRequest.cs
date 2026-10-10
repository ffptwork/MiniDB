using System.ComponentModel.DataAnnotations;

namespace MiniDB.Server.Contracts;

public sealed class ColumnRequest
{
    [Required]
    public required string Name { get; init; }

    [Required]
    public required string DataTypeName { get; init; }
}
