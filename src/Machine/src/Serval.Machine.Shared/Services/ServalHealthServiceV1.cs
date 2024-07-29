using Google.Protobuf.WellKnownTypes;
using Serval.Health.V1;

namespace Serval.Machine.Shared.Services;

public class ServalHealthServiceV1(HealthCheckService healthCheckService) : HealthApi.HealthApiBase
{
    private readonly HealthCheckService _healthCheckService = healthCheckService;

    public override async Task<HealthCheckResponse> HealthCheck(Empty request, ServerCallContext context)
    {
        HealthReport healthReport = await _healthCheckService.CheckHealthAsync();
        HealthCheckResponse healthCheckResponse = WriteGrpcHealthCheckResponse.Generate(healthReport);
        return healthCheckResponse;
    }
}
