namespace Serval.Assessment.Contracts;

public record AssessmentCorpusUpdateConfigDto
{
    public required IReadOnlyList<AssessmentCorpusFileConfigDto> Files { get; init; }
}
