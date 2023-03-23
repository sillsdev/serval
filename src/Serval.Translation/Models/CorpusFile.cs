namespace Serval.Translation.Models;

public class CorpusFile
{
    public string Id { get; set; } = default!;
    public string Filename { get; set; } = default!;
    public DataFileFormat Format { get; set; } = default!;
    public string TextId { get; set; } = default!;
}
