namespace Serval.ApiServer.Controllers;

public class HealthReportEntryDto
{
    public string Status { get; set; } = default!;
    public string Duration { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Exception { get; set; } = default!;
    public string Data { get; set; } = default!;
}
