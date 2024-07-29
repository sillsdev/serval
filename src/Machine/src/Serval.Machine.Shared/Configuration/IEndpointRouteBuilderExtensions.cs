namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServalTranslationEngineService(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<ServalTranslationEngineServiceV1>();
        builder.MapGrpcService<ServalHealthServiceV1>();

        return builder;
    }
}
