namespace Serval.Shared.Contracts;

public record CorpusUpdated
{
    public required string CorpusId { get; init; }
    public required IReadOnlyList<CorpusFileResult> Files { get; init; }
}
