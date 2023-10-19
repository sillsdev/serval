namespace Serval.Translation.Models;

public class Build : IEntity
{
    public string Id { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string? Name { get; set; }
    public string EngineRef { get; set; } = default!;
    public List<PretranslateCorpus>? Pretranslate { get; set; }
    public int Step { get; set; }
    public double? PercentCompleted { get; set; }
    public string? Message { get; set; }
    public int? QueueDepth { get; set; }
    public JobState State { get; set; } = JobState.Pending;
    public DateTime? DateFinished { get; set; }
    public IDictionary<string, object>? Options { get; set; }
}
