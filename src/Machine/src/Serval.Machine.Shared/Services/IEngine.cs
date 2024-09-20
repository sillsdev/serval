namespace Serval.Shared.Models;

public interface IEngine : IEntity
{
    public string? Name { get; init; }
    public EngineType Type { get; init; }
    public bool IsJobRunning { get; init; }
    public int JobRevision { get; init; }
}
