namespace Serval.ApiServer.Contracts;

public class HealthReportEntryDto
{
    public string Status { get; set; } = default!;
    public string Duration { get; set; } = default!;
    public string? Description { get; set; } = null;
    public string? Exception { get; set; } = null;
    public IDictionary<string, string>? Data { get; set; } = null;
}
