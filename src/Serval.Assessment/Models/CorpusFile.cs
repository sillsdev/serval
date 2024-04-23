namespace Serval.Assessment.Models;

public record CorpusFile
{
    public required string Id { get; init; }
    public required string Filename { get; init; }
    public required FileFormat Format { get; init; }
    public required string TextId { get; init; }
}
