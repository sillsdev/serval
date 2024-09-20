namespace Serval.Shared.Contracts;

public record CorpusFileConfigDto
{
    public required string FileId { get; init; }

    public string? TextId { get; init; }
}
