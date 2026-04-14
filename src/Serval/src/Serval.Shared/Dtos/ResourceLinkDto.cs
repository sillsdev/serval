namespace Serval.Shared.Dtos;

public record ResourceLinkDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
}
