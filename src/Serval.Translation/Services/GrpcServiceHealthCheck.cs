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
        HealthCheckResponse? healthCheckResponse = await client.HealthCheckAsync(
            new Google.Protobuf.WellKnownTypes.Empty(),
            cancellationToken: cancellationToken
        );
        if (healthCheckResponse is null)
            return HealthCheckResult.Unhealthy(
                $"Health check for {context.Registration.Name} failed with response null"
            );
        // map health report to health check result
        HealthCheckResult healthCheckResult =
            new(
                status: (HealthStatus)healthCheckResponse.Status,
                description: context.Registration.Name,
                exception: (healthCheckResponse.Error is null || healthCheckResponse.Error == "")
                    ? null
                    : new Exception(healthCheckResponse.Error),
                data: healthCheckResponse.Data.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
            );
        return healthCheckResult;
    }
}
