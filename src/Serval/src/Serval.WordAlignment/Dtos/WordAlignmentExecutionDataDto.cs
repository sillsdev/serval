namespace Serval.WordAlignment.Dtos;

public record WordAlignmentExecutionDataDto
{
    public int TrainCount { get; init; }
    public int WordAlignCount { get; init; }
    public bool? IsTrainFilteredByChapter { get; init; }
    public bool? IsWordAlignFilteredByChapter { get; init; }
    public IReadOnlyDictionary<string, Dictionary<string, int>>? TrainVerseCount { get; init; }
    public IReadOnlyDictionary<string, Dictionary<string, int>>? WordAlignVerseCount { get; init; }

    [Obsolete]
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<DiagnosticDto>? Diagnostics { get; init; }
    public string? EngineSourceLanguageTag { get; init; }
    public string? EngineTargetLanguageTag { get; init; }
}
