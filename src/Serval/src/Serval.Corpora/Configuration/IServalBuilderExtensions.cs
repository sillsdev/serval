namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddCorpora(this IServalBuilder builder)
    {
        builder.Services.AddScoped<ICorpusService, CorpusService>();
        return builder;
    }
}
