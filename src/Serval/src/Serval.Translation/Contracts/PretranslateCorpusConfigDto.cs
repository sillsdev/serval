namespace Serval.Translation.Contracts;

public record PretranslateCorpusConfigDto
{
    [Obsolete]
    public string? CorpusId { get; init; }

    [Obsolete]
    public IReadOnlyList<string>? TextIds { get; init; }

    [Obsolete]
    public string? ScriptureRange { get; init; }

    public string? ParallelCorpusId { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? SourceFilters { get; init; }
}
