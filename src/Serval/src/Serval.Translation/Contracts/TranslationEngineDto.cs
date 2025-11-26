namespace Serval.Translation.Contracts;

public record TranslationEngineDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public string? Name { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required string Type { get; init; }
    public bool? IsModelPersisted { get; init; }
    public required bool IsBuilding { get; init; }
    public required int ModelRevision { get; init; }
    public required double Confidence { get; init; }
    public required int CorpusSize { get; init; }
    public DateTime? DateCreated { get; init; }
}
