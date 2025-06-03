namespace SIL.ServiceToolkit.Models;

public enum BuildPhaseStage
{
    Train,
    Inference
}

public record BuildPhase
{
    public required BuildPhaseStage Stage { get; init; }
    public required int Step { get; init; }
    public required int StepCount { get; init; }
}
