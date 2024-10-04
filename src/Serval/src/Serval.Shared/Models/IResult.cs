namespace Serval.Shared.Models;

public interface IResult : IEntity
{
    public string? Name { get; init; }
    public string EngineRef { get; init; }
}
