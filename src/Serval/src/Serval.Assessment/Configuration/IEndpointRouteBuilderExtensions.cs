namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServalAssessmentServices(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<AssessmentPlatformServiceV1>();

        return builder;
    }
}
