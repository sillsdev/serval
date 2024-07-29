namespace Serval.Assessment.Contracts;

public record AssessmentEngineDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public string? Name { get; init; }
    public required string Type { get; init; }
    public required AssessmentCorpusDto Corpus { get; init; }
    public AssessmentCorpusDto? ReferenceCorpus { get; init; }
}
