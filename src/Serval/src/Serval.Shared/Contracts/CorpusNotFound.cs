namespace Serval.Shared.Contracts;

public record CorpusNotFound
{
    public required string CorpusId { get; init; }
    public required string Owner { get; init; }
}
