namespace Serval.ApiServer.Dtos;

public record HealthReportEntryDto
{
    public required string Status { get; init; }
    public required string Duration { get; init; }
    public string? Description { get; init; }
    public string? Exception { get; init; }
    public IDictionary<string, string>? Data { get; init; }
}
