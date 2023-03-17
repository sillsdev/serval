namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServalTranslationServices(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<TranslationServiceV1>();

        return builder;
    }
}
