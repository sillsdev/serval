namespace Serval.Shared.Contracts;

public record BuildProgressStatus
{
    public int Step { get; set; }
    public double? PercentCompleted { get; set; }
    public string? Message { get; set; }
}
