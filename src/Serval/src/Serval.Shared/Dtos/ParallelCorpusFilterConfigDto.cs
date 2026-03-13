namespace Serval.Shared.Dtos;

public record ParallelCorpusFilterConfigDto
{
    public required string CorpusId { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
}
