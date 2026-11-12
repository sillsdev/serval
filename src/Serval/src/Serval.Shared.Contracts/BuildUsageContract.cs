namespace Serval.Shared.Contracts;

public record BuildUsageContract
{
    public decimal TotalCostCredits { get; init; }
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
}
