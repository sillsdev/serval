namespace Serval.Translation.Models;

public record Enhancement
{
    public required string Type { get; init; }
    public string? Model { get; init; }
}
