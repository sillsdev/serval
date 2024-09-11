namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required ResourceLinkDto Engine { get; init; }

    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required IReadOnlyList<ResourceLinkDto> SourceCorporaRefs { get; init; } = new List<ResourceLinkDto>();
    public required IReadOnlyList<ResourceLinkDto> TargetCorporaRefs { get; init; } = new List<ResourceLinkDto>();
}
