namespace Serval.Translation.Contracts;

public record CorpusFilterConfigDto
{
    public required string CorpusId { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
}
