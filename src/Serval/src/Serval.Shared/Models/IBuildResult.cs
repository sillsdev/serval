namespace Serval.Shared.Models;

public interface IBuildResult : IEntity
{
    public string EngineRef { get; init; }
    public int BuildRevision { get; init; }
}
