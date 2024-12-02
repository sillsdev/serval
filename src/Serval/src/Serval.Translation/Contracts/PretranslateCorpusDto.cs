namespace Serval.Translation.Contracts;

public record PretranslateCorpusDto
{
    [Obsolete]
    public ResourceLinkDto? Corpus { get; init; }

    [Obsolete]
    public IReadOnlyList<string>? TextIds { get; init; }

    [Obsolete]
    public string? ScriptureRange { get; init; }

    public ResourceLinkDto? ParallelCorpus { get; init; }
    public IReadOnlyList<ParallelCorpusFilterDto>? SourceFilters { get; init; }
}
