namespace Serval.Translation.Contracts;

public record ExecutionDataContract
{
    public int? TrainCount { get; init; }
    public int? PretranslateCount { get; init; }
    public bool? IsTrainFilteredByChapter { get; init; }
    public bool? IsPretranslateFilteredByChapter { get; init; }
    public Dictionary<string, Dictionary<string, int>>? TrainVerseCount { get; init; }
    public Dictionary<string, Dictionary<string, int>>? PretranslateVerseCount { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public string? EngineSourceLanguageTag { get; init; }
    public string? EngineTargetLanguageTag { get; init; }
    public string? ResolvedSourceLanguage { get; init; }
    public string? ResolvedTargetLanguage { get; init; }
}
