namespace Serval.Translation.Contracts;

public class ParallelCorpusFileDto
{
    public ResourceLinkDto File { get; set; } = default!;
    public string? TextId { get; set; }
}
