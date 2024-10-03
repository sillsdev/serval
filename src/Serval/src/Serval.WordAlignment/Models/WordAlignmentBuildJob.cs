namespace Serval.WordAlignment.Models;

public record WordAlignmentBuildJob : IJob
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public string? Name { get; init; }
    public required string EngineRef { get; init; }
    public double? PercentCompleted { get; init; }
    public string? Message { get; init; }
    public JobState State { get; init; } = JobState.Pending;
    public DateTime? DateFinished { get; init; }
    public IReadOnlyDictionary<string, object>? Options { get; init; }
    public IReadOnlyList<FilteredCorpus>? TrainOn { get; init; }
    public IReadOnlyList<WordAlignmentCorpus>? AlignWordsOn { get; init; }
    public int Step { get; init; }
    public int? QueueDepth { get; init; }
}
