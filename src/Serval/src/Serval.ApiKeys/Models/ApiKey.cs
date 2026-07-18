namespace Serval.ApiKeys.Models;

public record ApiKey : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string ClientId { get; init; }
    public required string Name { get; init; }
    public required string HashedKey { get; init; }
    public required List<string> Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? RevokedAt { get; init; }
}
