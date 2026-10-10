using System.ComponentModel.DataAnnotations;

namespace MiniDB.Server.Contracts;

public sealed class EditRowRequest
{
    [Required]
    public required Dictionary<string, string> Values { get; init; }
}
