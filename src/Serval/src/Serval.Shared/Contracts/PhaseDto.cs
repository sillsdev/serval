namespace Serval.Shared.Contracts;

public record PhaseDto
{
    public required PhaseStage Stage { get; init; }
    public required int Step { get; init; }
    public required int StepCount { get; init; }
}
