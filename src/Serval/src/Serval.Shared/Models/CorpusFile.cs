namespace Serval.Shared.Models;

public record CorpusFile
{
    public required string Id { get; set; }
    public required string Filename { get; set; }
    public required FileFormat Format { get; set; }
    public required string TextId { get; set; }
}
