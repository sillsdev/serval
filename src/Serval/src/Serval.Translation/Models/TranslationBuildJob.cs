namespace Serval.Translation.Models;

public record TranslationBuildJob : IJob
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public string? Name { get; init; }
    public required string EngineRef { get; init; }
    public int? QueueDepth { get; init; }
    public double? PercentCompleted { get; init; }
    public string? Message { get; init; }
    public JobState State { get; init; } = JobState.Pending;
    public DateTime? DateFinished { get; init; }
    public IReadOnlyDictionary<string, object>? Options { get; init; }
    public IReadOnlyList<FilteredCorpus>? TrainOn { get; init; }
    public IReadOnlyList<PretranslateCorpus>? Pretranslate { get; init; }
    public int Step { get; init; }
}
