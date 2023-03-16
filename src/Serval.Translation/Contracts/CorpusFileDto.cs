namespace Serval.Translation.Contracts;

public class CorpusFileDto
{
    public ResourceLinkDto File { get; set; } = default!;
    public string? TextId { get; set; }
}
