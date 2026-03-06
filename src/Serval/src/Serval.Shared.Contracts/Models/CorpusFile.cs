namespace Serval.Shared.Models;

public enum FileFormat
{
    Text = 0,
    Paratext = 1,
}

public record CorpusFile
{
    public required string Id { get; set; }
    public required string Filename { get; set; }
    public required FileFormat Format { get; set; }
    public required string TextId { get; set; }
}
