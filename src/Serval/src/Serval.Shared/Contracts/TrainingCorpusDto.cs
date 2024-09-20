namespace Serval.Shared.Contracts;

public record TrainingCorpusDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public string? Name { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required IReadOnlyList<CorpusFileDto> SourceFiles { get; init; }
    public required IReadOnlyList<CorpusFileDto> TargetFiles { get; init; }
}
