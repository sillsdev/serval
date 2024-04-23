namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServalAssessmentEngineService(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<ServalAssessmentEngineServiceV1>();
        builder.MapGrpcService<ServalHealthServiceV1>();

        return builder;
    }
}
