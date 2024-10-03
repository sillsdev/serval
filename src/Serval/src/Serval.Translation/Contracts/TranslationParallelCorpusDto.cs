namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required ResourceLinkDto Engine { get; init; }

    public required IReadOnlyList<TranslationMonolingualCorpusDto> SourceCorpora { get; init; } =
        new List<TranslationMonolingualCorpusDto>();
    public required IReadOnlyList<TranslationMonolingualCorpusDto> TargetCorpora { get; init; } =
        new List<TranslationMonolingualCorpusDto>();
}
