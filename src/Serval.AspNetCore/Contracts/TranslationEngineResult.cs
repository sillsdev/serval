namespace Serval.AspNetCore.Contracts;

public record TranslationEngineResult
{
    public string EngineId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string SourceLanguageTag { get; init; } = default!;
    public string TargetLanguageTag { get; set; } = default!;
    public string Type { get; init; } = default!;
    public bool IsBuilding { get; init; }
    public int ModelRevision { get; init; }
    public double Confidence { get; init; }
    public int CorpusSize { get; init; }
}
