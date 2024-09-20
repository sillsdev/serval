namespace Serval.Shared.Contracts;

public record TrainingCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required string SourceLanguage { get; init; }

    public required string TargetLanguage { get; init; }

    public required IReadOnlyList<CorpusFileConfigDto> SourceFiles { get; init; }

    public required IReadOnlyList<CorpusFileConfigDto> TargetFiles { get; init; }
}
