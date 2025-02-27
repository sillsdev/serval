namespace Serval.Shared.Contracts;

public record ParallelCorpusFilterDto
{
    public required ResourceLinkDto Corpus { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
}
