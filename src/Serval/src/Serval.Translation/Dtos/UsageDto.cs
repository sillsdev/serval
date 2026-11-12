namespace Serval.Translation.Dtos;

public record UsageDto
{
    public decimal TotalCostCredits { get; init; }
    public decimal CostLimitCredits { get; init; }
}
