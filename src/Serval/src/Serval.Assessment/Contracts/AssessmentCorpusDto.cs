namespace Serval.Assessment.Contracts;

public record AssessmentCorpusDto
{
    public required string Url { get; init; }
    public string? Name { get; init; }
    public required string Language { get; init; }
    public required IReadOnlyList<AssessmentCorpusFileDto> Files { get; init; }
}
