namespace Serval.Shared.Models;

public interface IEngine : IOwnedEntity
{
    public string? Name { get; init; }
    public string Type { get; init; }
    public bool IsBuilding { get; init; }
    public int BuildRevision { get; init; }
}
