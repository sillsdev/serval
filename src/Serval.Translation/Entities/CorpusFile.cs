namespace Serval.Translation.Entities;

public class CorpusFile
{
    public string Id { get; set; } = default!;
    public string Filename { get; set; } = default!;
    public string Format { get; set; } = default!;
    public string? TextId { get; set; }
}
