namespace Serval.Machine.Shared.Configuration;

public class WordAlignmentEngineOptions
{
    public const string Key = "WordAlignmentEngine";

    public string EnginesDir { get; set; } = "word_alignment_engines";
    public TimeSpan EngineCommitFrequency { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan InactiveEngineTimeout { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan SaveModelTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan EngineCommitTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
