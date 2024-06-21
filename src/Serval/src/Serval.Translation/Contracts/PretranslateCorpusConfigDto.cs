namespace Serval.Translation.Contracts;

public record PretranslateCorpusConfigDto
{
    public required string CorpusId { get; init; }

    public IReadOnlyList<string>? TextIds { get; init; }

    public string? ScriptureRange { get; init; }
}
