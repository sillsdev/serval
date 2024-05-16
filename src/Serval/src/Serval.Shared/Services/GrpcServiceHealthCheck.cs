using Serval.Health.V1;

namespace Serval.Shared.Services;

public class GrpcServiceHealthCheck(GrpcClientFactory grpcClientFactory) : IHealthCheck
{
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        HealthApi.HealthApiClient client = _grpcClientFactory.CreateClient<HealthApi.HealthApiClient>(
            $"{context.Registration.Name}-Health"
        );
        HealthCheckResponse? healthCheckResponse = await client.HealthCheckAsync(
            new Google.Protobuf.WellKnownTypes.Empty(),
            cancellationToken: cancellationToken
        );
        if (healthCheckResponse is null)
        {
            return HealthCheckResult.Unhealthy(
                $"Health check for {context.Registration.Name} failed with response null"
            );
        }
        // map health report to health check result
        HealthCheckResult healthCheckResult =
            new(
                status: (HealthStatus)healthCheckResponse.Status,
                description: context.Registration.Name,
                exception: string.IsNullOrEmpty(healthCheckResponse.Error)
                    ? null
                    : new HealthCheckException(healthCheckResponse.Error),
                data: healthCheckResponse.Data.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
            );
        return healthCheckResult;
    }
}

public class HealthCheckException(string? message) : Exception(message) { }
