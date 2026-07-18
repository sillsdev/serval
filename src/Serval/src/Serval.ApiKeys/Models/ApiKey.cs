namespace Serval.ApiKeys.Models;

public record ApiKey : IOwnedEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Owner { get; init; }
    public required string Name { get; init; }
    public required string HashedKey { get; init; }
    public required List<string> Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
