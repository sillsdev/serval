namespace Serval.Aqua.Shared.Models;

public record Result
{
    public required string JobRef { get; init; }
    public required string TextId { get; init; }
    public required string Ref { get; init; }
    public required double Score { get; init; }
    public string? Description { get; init; }
}
