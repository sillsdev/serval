namespace Serval.Shared.Contracts;

public record CorpusFileDto
{
    public required ResourceLinkDto File { get; init; }
    public string? TextId { get; init; }
}
