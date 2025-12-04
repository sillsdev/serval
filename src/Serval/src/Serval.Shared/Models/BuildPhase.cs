namespace Serval.Shared.Models;

public enum BuildPhaseStage
{
    Train,
    Inference
}

public record BuildPhase
{
    public required BuildPhaseStage Stage { get; init; }
    public int? Step { get; init; }
    public int? StepCount { get; init; }
    public DateTime? Started { get; init; }
}
