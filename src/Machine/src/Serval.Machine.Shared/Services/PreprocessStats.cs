namespace Serval.Machine.Shared.Services;

public record PreprocessStats
{
    public int TrainCount { get; set; }
    public int InferenceCount { get; set; }
    public bool IsTrainFilteredByChapter { get; set; }
    public bool IsInferenceFilteredByChapter { get; set; }
    public Dictionary<string, Dictionary<string, int>> TrainVerseCount { get; set; } = [];
    public Dictionary<string, Dictionary<string, int>> InferenceVerseCount { get; set; } = [];
}
