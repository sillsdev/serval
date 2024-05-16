namespace Serval.Assessment.Contracts;

public record AssessmentCorpusFileDto
{
    public required ResourceLinkDto File { get; init; }
    public string? TextId { get; init; }
}
