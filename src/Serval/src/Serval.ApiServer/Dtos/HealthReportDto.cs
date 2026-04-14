namespace Serval.ApiServer.Dtos;

public record HealthReportDto
{
    public required string Status { get; init; }
    public required string TotalDuration { get; init; }
    public required IDictionary<string, HealthReportEntryDto> Results { get; init; }
}
