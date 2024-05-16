namespace Serval.Assessment.Contracts;

public record AssessmentEngineConfigDto
{
    /// <summary>
    /// The assessment engine name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The assessment engine type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The assessment engine corpus.
    /// </summary>
    public required string CorpusId { get; init; }

    /// <summary>
    /// The assessment engine reference corpus.
    /// </summary>
    public string? ReferenceCorpusId { get; init; }
}
