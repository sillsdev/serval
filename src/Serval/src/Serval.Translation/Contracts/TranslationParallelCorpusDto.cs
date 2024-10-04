namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public required IReadOnlyList<ResourceLinkDto> SourceCorpora { get; init; }
    public required IReadOnlyList<ResourceLinkDto> TargetCorpora { get; init; }
}
