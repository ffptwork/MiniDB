namespace MiniDB.Web.Models;

public sealed class ProblemDetailsDto
{
    public string? Title { get; init; }
    public string? Detail { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}
