namespace Serval.WordAlignment.Contracts;

public record WordAlignOnCorpusConfigDto
{
    public required string CorpusId { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
}
