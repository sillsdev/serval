namespace Serval.Assessment.Contracts;

public record AssessmentCorpusFileConfigDto
{
    public required string FileId { get; init; }

    public string? TextId { get; init; }
}
