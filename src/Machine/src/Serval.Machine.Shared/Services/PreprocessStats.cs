using SIL.Extensions;

namespace Serval.Machine.Shared.Services;

public record PreprocessStats
{
    public int TrainCount { get; set; }
    public int InferenceCount { get; set; }
    public bool IsTrainFilteredByChapter { get; set; }
    public bool IsInferenceFilteredByChapter { get; set; }
    public Dictionary<string, Dictionary<string, int>> TrainVerseCount { get; set; } = [];
    public Dictionary<string, Dictionary<string, int>> InferenceVerseCount { get; set; } = [];

    public void UpdateTrainCount(ParallelRowContract row)
    {
        TrainCount++;
        UpdateVerseCount(TrainVerseCount, row);
    }

    public void UpdateInferenceCount(ParallelRowContract row)
    {
        InferenceCount++;
        UpdateVerseCount(InferenceVerseCount, row);
    }

    private static void UpdateVerseCount(
        Dictionary<string, Dictionary<string, int>> verseCounts,
        ParallelRowContract row
    )
    {
        foreach (object? reference in row.SourceRefs)
        {
            if (reference is not null and ScriptureRef sr && sr.IsVerse)
            {
                verseCounts.UpdateValue(
                    sr.Book,
                    () => [],
                    chapters =>
                    {
                        if (chapters.TryGetValue(sr.Chapter, out int count))
                            chapters[sr.Chapter] = count + 1;
                        else
                            chapters[sr.Chapter] = 1;
                        return chapters;
                    }
                );
            }
        }
    }
}
