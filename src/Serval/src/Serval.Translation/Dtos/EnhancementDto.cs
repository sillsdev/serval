namespace Serval.Translation.Dtos;

public record EnhancementDto
{
    public required string Type { get; init; }
    public string? Model { get; init; }
}
