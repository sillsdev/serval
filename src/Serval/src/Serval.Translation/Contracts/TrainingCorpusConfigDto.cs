namespace Serval.Translation.Contracts;

public record TrainingCorpusConfigDto
{
    public string? CorpusId { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }

    public string? ParallelCorpusId { get; init; }
    public ParallelCorpusFilterConfigDto? SourceFilter { get; init; }
    public ParallelCorpusFilterConfigDto? TargetFilter { get; init; }
}
