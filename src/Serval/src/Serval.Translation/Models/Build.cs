namespace Serval.Translation.Models;

public record Build : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public string? Name { get; init; }
    public required string EngineRef { get; init; }
    public IReadOnlyList<TrainingCorpus>? TrainOn { get; init; }
    public IReadOnlyList<PretranslateCorpus>? Pretranslate { get; init; }
    public int Step { get; init; }
    public double? Progress { get; init; }
    public string? Message { get; init; }
    public int? QueueDepth { get; init; }
    public JobState State { get; init; } = JobState.Pending;
    public DateTime? DateFinished { get; init; }
    public IReadOnlyDictionary<string, object>? Options { get; init; }
    public string? DeploymentVersion { get; init; }
    public IReadOnlyDictionary<string, string> ExecutionData { get; init; } = new Dictionary<string, string>();
    public DateTime? DateCreated { get; set; }
    public IReadOnlyList<BuildPhase>? Phases { get; init; }
    public IReadOnlyList<ParallelCorpusAnalysis>? Analysis { get; init; }
}
