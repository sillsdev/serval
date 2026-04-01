namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddTranslationEngine<TEngineService>(this IServalBuilder builder, string engineType)
        where TEngineService : class, ITranslationEngineService
    {
        builder.Services.AddKeyedScoped<ITranslationEngineService, TEngineService>(engineType.ToCamelCase());
        return builder;
    }
}
