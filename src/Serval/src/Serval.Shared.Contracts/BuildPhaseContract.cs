namespace Serval.Shared.Contracts;

public enum BuildPhaseStage
{
    Train,
    Inference,
}

public record BuildPhaseContract
{
    public required BuildPhaseStage Stage { get; init; }
    public int? Step { get; init; }
    public int? StepCount { get; init; }
    public DateTime? Started { get; init; }
}
