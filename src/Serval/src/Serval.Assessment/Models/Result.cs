namespace Serval.Assessment.Models;

public record Result : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string EngineRef { get; init; }
    public required string JobRef { get; init; }
    public required string TextId { get; init; }
    public required string Ref { get; init; }
    public double? Score { get; init; }
    public string? Description { get; init; }
}
