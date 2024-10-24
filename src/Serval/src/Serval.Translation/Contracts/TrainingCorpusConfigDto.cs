namespace Serval.Translation.Contracts;

public record TrainingCorpusConfigDto
{
    public string? CorpusId { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }

    public string? ParallelCorpusId { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? SourceFilters { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? TargetFilters { get; init; }
}
