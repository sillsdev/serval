namespace Serval.WordAlignment.Contracts;

public record WordAlignOnCorpusDto
{
    public required ResourceLinkDto Corpus { get; init; }

    public IReadOnlyList<string>? TextIds { get; init; }

    public string? ScriptureRange { get; init; }
}
