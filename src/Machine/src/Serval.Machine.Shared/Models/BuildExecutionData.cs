namespace Serval.Machine.Shared.Models;

public record BuildExecutionData
{
    public int? TrainCount { get; init; }
    public int? InferenceCount { get; init; }
    public bool? IsTrainFilteredByChapter { get; init; }
    public bool? IsInferenceFilteredByChapter { get; init; }
    public Dictionary<string, Dictionary<string, int>>? TrainVerseCount { get; init; }
    public Dictionary<string, Dictionary<string, int>>? InferenceVerseCount { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public string? EngineSourceLanguageTag { get; init; }
    public string? EngineTargetLanguageTag { get; init; }
    public string? ResolvedSourceLanguage { get; init; }
    public string? ResolvedTargetLanguage { get; init; }
}
