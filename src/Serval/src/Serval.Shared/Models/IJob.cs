namespace Serval.Shared.Models;

public interface IJob : IEntity
{
    public string? Name { get; init; }
    public string EngineRef { get; init; }
    public double? PercentCompleted { get; init; }
    public string? Message { get; init; }
    public JobState State { get; init; }
    public DateTime? DateFinished { get; init; }
    public IReadOnlyDictionary<string, object>? Options { get; init; }
}
