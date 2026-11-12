namespace Serval.Translation.Dtos;

public record BuildUsageDto
{
    public decimal TotalCostCredits { get; init; }
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
}
