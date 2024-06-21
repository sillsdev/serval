namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServalTranslationServices(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<TranslationPlatformServiceV1>();

        return builder;
    }
}
