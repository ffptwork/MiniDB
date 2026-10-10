using System.ComponentModel.DataAnnotations;

namespace MiniDB.Server.Contracts;

public sealed class SearchRequest
{
    [Required]
    public string ColumnName { get; init; } = null!;

    [Required(AllowEmptyStrings = true)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Pattern { get; init; } = null!;
}
