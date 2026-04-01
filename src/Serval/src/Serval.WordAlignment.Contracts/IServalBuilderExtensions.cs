namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWordAlignmentEngine<TEngineService>(this IServalBuilder builder, string engineType)
        where TEngineService : class, IWordAlignmentEngineService
    {
        builder.Services.AddKeyedScoped<IWordAlignmentEngineService, TEngineService>(engineType.ToLowerInvariant());
        return builder;
    }
}
