namespace Serval.DataFiles.Messages;

public record GetCorpus
{
    public required string CorpusId { get; init; }
    public required string Owner { get; init; }
}
