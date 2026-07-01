namespace Serval.WordAlignment.Contracts;

public record ExecutionDataContract
{
    public int? TrainCount { get; init; }
    public int? WordAlignCount { get; init; }
    public bool? IsTrainFilteredByChapter { get; init; }
    public bool? IsWordAlignmentFilteredByChapter { get; init; }
    public Dictionary<string, Dictionary<string, int>>? TrainVerseCount { get; init; }
    public Dictionary<string, Dictionary<string, int>>? WordAlignVerseCount { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public string? EngineSourceLanguageTag { get; init; }
    public string? EngineTargetLanguageTag { get; init; }
}
