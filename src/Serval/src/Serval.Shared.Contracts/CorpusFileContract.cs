namespace Serval.Shared.Contracts;

public record CorpusFileContract
{
    public required string Location { get; init; }
    public required FileFormat Format { get; init; }
    public required string TextId { get; init; }
}
