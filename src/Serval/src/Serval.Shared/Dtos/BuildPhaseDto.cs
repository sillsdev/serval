namespace Serval.Shared.Contracts;

public record BuildPhaseDto
{
    public required BuildPhaseStage Stage { get; init; }
    public int? Step { get; init; }
    public int? StepCount { get; init; }
    public DateTime? Started { get; init; }
}
