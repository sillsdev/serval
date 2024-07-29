namespace Serval.Assessment.Contracts;

public record AssessmentCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The language tag.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// The corpus files.
    /// </summary>
    public required IReadOnlyList<AssessmentCorpusFileConfigDto> Files { get; init; }
}
