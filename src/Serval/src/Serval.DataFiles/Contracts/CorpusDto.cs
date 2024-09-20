namespace Serval.Corpora.Contracts;

public record CorpusDto
{
    public required string Id { get; init; }
    public required int Revision { get; init; }
    public required string Language { get; init; }
    public string? Name { get; init; }
    public required string Url { get; init; }
    public required IReadOnlyList<CorpusFileDto> Files { get; set; }
}
