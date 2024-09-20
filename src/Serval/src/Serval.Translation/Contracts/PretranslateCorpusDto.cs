namespace Serval.Translation.Contracts;

public record PretranslateCorpusDto
{
    public ResourceLinkDto? Corpus { get; init; }

    public IReadOnlyList<string>? TextIds { get; init; }

    public string? ScriptureRange { get; init; }

    public ResourceLinkDto? ParallelCorpus { get; init; }
    public IReadOnlyList<ParallelCorpusFilterDto>? SourceFilters { get; init; }
    public IReadOnlyList<ParallelCorpusFilterDto>? TargetFilters { get; init; }
}
