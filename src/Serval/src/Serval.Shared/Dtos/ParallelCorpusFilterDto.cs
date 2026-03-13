namespace Serval.Shared.Dtos;

public record ParallelCorpusFilterDto
{
    public required ResourceLinkDto Corpus { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
}
