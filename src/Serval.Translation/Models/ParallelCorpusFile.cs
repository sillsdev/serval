namespace Serval.Translation.Models;

public class ParallelCorpusFile
{
    public string Id { get; set; } = default!;
    public string Filename { get; set; } = default!;
    public FileFormat Format { get; set; } = default!;
    public string TextId { get; set; } = default!;
}
