namespace Serval.ApiServer.Controllers;

public class HealthReportDto
{
    public string Status { get; set; } = default!;
    public string TotalDuration { get; set; } = default!;
    public IDictionary<string, HealthReportEntryDto> Entries { get; set; } = default!;
}