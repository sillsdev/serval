namespace Serval.Assessment.Contracts;

public record AssessmentEngineDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public string? Name { get; init; }
    public required string Type { get; init; }
    public required ResourceLinkDto Corpus { get; init; }
    public ResourceLinkDto? ReferenceCorpus { get; init; }
}
