namespace Serval.ApiServer.Contracts;

public class HealthReportDto
{
    public string Status { get; set; } = default!;
    public string TotalDuration { get; set; } = default!;
    public IDictionary<string, HealthReportEntryDto> Results { get; set; } = default!;
}
