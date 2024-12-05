namespace Serval.DataFiles.Contracts;

public record CorpusFileDto
{
    public required string FileId { get; init; }
    public string? TextId { get; init; }
}
