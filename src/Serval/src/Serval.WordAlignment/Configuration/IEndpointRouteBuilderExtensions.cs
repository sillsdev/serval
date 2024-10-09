namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServalWordAlignmentServices(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<WordAlignmentPlatformServiceV1>();

        return builder;
    }
}
