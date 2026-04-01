namespace Serval.Shared.Contracts;

public record PhaseContract
{
    public required PhaseStage Stage { get; init; }
    public int? Step { get; init; }
    public int? StepCount { get; init; }
    public DateTime? Started { get; init; }
}
