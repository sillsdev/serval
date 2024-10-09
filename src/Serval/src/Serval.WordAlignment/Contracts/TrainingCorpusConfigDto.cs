namespace Serval.WordAlignment.Contracts;

public record TrainingCorpusConfigDto
{
    public required string CorpusId { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
}
