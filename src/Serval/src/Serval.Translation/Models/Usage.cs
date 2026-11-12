namespace Serval.Translation.Models;

public record Usage : IOwnedEntity
{
    public string Id { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public required string Owner { get; init; }
    public decimal TotalCostCredits { get; init; }
    public decimal CostLimitCredits { get; init; }
}
