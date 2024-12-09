namespace Serval.WordAlignment.Contracts;

public record TrainingCorpusDto
{
    public ResourceLinkDto? ParallelCorpus { get; init; }
    public IReadOnlyList<ParallelCorpusFilterDto>? SourceFilters { get; init; }
    public IReadOnlyList<ParallelCorpusFilterDto>? TargetFilters { get; init; }
}
