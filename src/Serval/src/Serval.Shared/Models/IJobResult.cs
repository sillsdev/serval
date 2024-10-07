namespace Serval.Shared.Models;

public interface IJobResult : IEntity
{
    public string EngineRef { get; init; }
    public int JobRevision { get; init; }
}
