using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Serval.Translation.V1;

public class WriteGrpcHealthCheckResponse
{
    public static HealthCheckResponse Generate(HealthReport healthReport)
    {
        Dictionary<string, string> healthCheckResultData = [];
        string? healthCheckResultException = null;

        // Combine data and exceptions from all health checks
        foreach (KeyValuePair<string, HealthReportEntry> entry in healthReport.Entries)
        {
            healthCheckResultData.Add(entry.Key, $"{entry.Value.Status}: {entry.Value.Description ?? ""}");
            if ((entry.Value.Exception?.ToString() ?? "") != "")
                if (healthCheckResultException is null)
                    healthCheckResultException = $"{entry.Key}: {entry.Value.Exception}";
                else
                    healthCheckResultException += $"\n{entry.Key}: {entry.Value.Exception}";
        }
        // Assemble response
        HealthCheckResponse healthCheckReponse =
            new() { Status = (HealthCheckStatus)healthReport.Status, Error = healthCheckResultException ?? "" };
        foreach (KeyValuePair<string, string> entry in healthCheckResultData)
        {
            healthCheckReponse.Data.Add(entry.Key, entry.Value);
        }
        return healthCheckReponse;
    }
}