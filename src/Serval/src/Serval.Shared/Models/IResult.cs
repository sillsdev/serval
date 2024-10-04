namespace Serval.Shared.Models;

public interface IResult : IEntity
{
    public string EngineRef { get; init; }
    public int JobRevision { get; init; }
}
