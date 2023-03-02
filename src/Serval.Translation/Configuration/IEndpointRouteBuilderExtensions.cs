namespace Microsoft.AspNetCore.Builder;

public static class IEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServalTranslationServices(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<TranslationService>();

        return builder;
    }
}
