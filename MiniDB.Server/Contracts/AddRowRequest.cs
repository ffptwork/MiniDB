using System.ComponentModel.DataAnnotations;

namespace MiniDB.Server.Contracts;

public sealed class AddRowRequest
{
    [Required]
    public required Dictionary<string, string> Values { get; init; }
}
