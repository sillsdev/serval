namespace Serval.Translation.Contracts;

public record PretranslateCorpusDto
{
    public required ResourceLinkDto Corpus { get; init; }

    public IReadOnlyList<string>? TextIds { get; init; }

    public string? ScriptureRange { get; init; }
}
