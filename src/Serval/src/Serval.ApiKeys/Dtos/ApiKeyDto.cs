namespace Serval.ApiKeys.Dtos;

public record ApiKeyDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string Owner { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public required DateTime CreatedAt { get; init; }
}
