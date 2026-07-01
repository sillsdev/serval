namespace Serval.Machine.Shared.Services;

public record PreprocessStats
{
    public required int TrainCount { get; init; }
    public required int InferenceCount { get; init; }
    public required bool IsTrainFilteredByChapter { get; init; }
    public required bool IsInferenceFilteredByChapter { get; init; }
    public required Dictionary<string, Dictionary<string, int>> TrainVerseCount { get; init; }
    public required Dictionary<string, Dictionary<string, int>> InferenceVerseCount { get; init; }
}
