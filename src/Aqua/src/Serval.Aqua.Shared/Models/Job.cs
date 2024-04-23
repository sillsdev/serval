namespace Serval.Aqua.Shared.Models;

public enum JobStageState
{
    Pending,
    Active,
    Canceling
}

public enum JobStage
{
    Preprocess,
    Assess,
    Postprocess
}

public record Job : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string EngineRef { get; init; }
    public JobStageState? StageState { get; init; }
    public string? StageId { get; init; }
    public JobStage? Stage { get; init; }
    public string? Options { get; init; }
    public required CorpusFilter CorpusFilter { get; init; }
}
