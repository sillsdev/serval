using Grpc.Health.V1;

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
        var client = _grpcClientFactory.CreateClient<Health.HealthClient>(context.Registration.Name + "_Health");
        try
        {
            HealthCheckResponse response = await client.CheckAsync(
                new HealthCheckRequest(),
                cancellationToken: cancellationToken
            );
            return response.Status is HealthCheckResponse.Types.ServingStatus.NotServing
                ? HealthCheckResult.Degraded()
                : HealthCheckResult.Healthy();
        }
        catch (RpcException e)
        {
            if (e.Status.StatusCode is StatusCode.Unimplemented)
                return HealthCheckResult.Healthy("Health checking is not implemented.");
            return HealthCheckResult.Degraded(
                description: e.Status.DebugException?.Message ?? e.Status.Detail,
                exception: e
            );
        }
    }
}
