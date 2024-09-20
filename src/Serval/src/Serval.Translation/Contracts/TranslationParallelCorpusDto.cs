namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required ResourceLinkDto Engine { get; init; }

    public required IReadOnlyList<TranslationParallelCorpusSubcorpusDto> SourceCorpora { get; init; } =
        new List<TranslationParallelCorpusSubcorpusDto>();
    public required IReadOnlyList<TranslationParallelCorpusSubcorpusDto> TargetCorpora { get; init; } =
        new List<TranslationParallelCorpusSubcorpusDto>();
}
