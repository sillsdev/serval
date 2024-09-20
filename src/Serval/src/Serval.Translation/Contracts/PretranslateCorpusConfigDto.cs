namespace Serval.Translation.Contracts;

public record PretranslateCorpusConfigDto //TODO do we really need the parallel Translate/Pretranslate corpora classes since identical? Maybe `FilteredCorpus...`?
{
    public string? CorpusId { get; init; }

    public IReadOnlyList<string>? TextIds { get; init; }

    public string? ScriptureRange { get; init; }

    public string? ParallelCorpusId { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? SourceFilters { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? TargetFilters { get; init; }
}
