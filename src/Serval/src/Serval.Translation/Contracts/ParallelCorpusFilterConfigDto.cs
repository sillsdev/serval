namespace Serval.Translation.Contracts;

public record ParallelCorpusFilterConfigDto
{
    public required string CorpusRef { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
}
