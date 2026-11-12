namespace Serval.Translation.Models;

public record BuildUsage
{
    public decimal TotalCostCredits { get; init; }
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
}
