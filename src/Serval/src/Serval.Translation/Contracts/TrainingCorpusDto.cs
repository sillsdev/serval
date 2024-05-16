namespace Serval.Translation.Contracts;

public record TrainingCorpusDto
{
    public required ResourceLinkDto Corpus { get; init; }

    public IReadOnlyList<string>? TextIds { get; init; }

    public string? ScriptureRange { get; init; }
}
