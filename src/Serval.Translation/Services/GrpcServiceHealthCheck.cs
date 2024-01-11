using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serval.Translation.V1;

namespace Serval.Shared.Services;

public class GrpcServiceHealthCheck : IHealthCheck
{
    private readonly GrpcClientFactory _grpcClientFactory;

    public GrpcServiceHealthCheck(GrpcClientFactory grpcClientFactory)
    {
        _grpcClientFactory = grpcClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(
            context.Registration.Name
        );
        HealthCheckResponse? healthReport = await client.HealthCheckAsync(
            new Google.Protobuf.WellKnownTypes.Empty(),
            cancellationToken: cancellationToken
        );
        if (healthReport is null)
            return HealthCheckResult.Unhealthy(
                $"Health check for {context.Registration.Name} failed with response null"
            );
        // map health report to health check result
        HealthCheckResult healthCheckResult =
            new(
                status: (HealthStatus)healthReport.Status,
                description: context.Registration.Name,
                exception: healthReport.Exception is null ? null : new Exception(healthReport.Exception),
                data: healthReport.Data.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
            );
        return healthCheckResult;
    }
}
