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
    /// The corpus.
    /// </summary>
    public required AssessmentCorpusConfigDto Corpus { get; init; }

    /// <summary>
    /// The reference corpus.
    /// </summary>
    public AssessmentCorpusConfigDto? ReferenceCorpus { get; init; }
}
