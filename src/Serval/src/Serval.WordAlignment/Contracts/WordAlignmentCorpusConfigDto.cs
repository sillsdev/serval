namespace Serval.WordAlignment.Contracts;

public record WordAlignmentCorpusConfigDto
{
    public string? ParallelCorpusId { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? SourceFilters { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? TargetFilters { get; init; }
}
