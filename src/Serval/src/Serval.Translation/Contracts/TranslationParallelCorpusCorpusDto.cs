namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusSubcorpusDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Language { get; set; }
    public required IReadOnlyList<TranslationCorpusFileDto> Files { get; set; }
}
