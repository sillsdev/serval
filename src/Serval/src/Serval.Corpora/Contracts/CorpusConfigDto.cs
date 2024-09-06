namespace Serval.Corpora.Contracts;

public record CorpusConfigDto
{
    public string? Name { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyList<CorpusFileConfigDto> Files { get; init; }
}
