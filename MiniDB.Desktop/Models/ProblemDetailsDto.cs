namespace MiniDB.Desktop.Models;

internal sealed class ProblemDetailsDto
{
    public string? Title { get; init; }
    public string? Detail { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}
