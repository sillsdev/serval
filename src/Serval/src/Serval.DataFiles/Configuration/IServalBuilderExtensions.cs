namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddDataFiles(this IServalBuilder builder)
    {
        builder.AddDataFileOptions(builder.Configuration.GetSection(DataFileOptions.Key));

        builder.Services.AddScoped<IDataFileService, DataFileService>();
        builder.Services.AddHostedService<DeletedFileCleaner>();

        builder.Services.AddScoped<ICorpusService, CorpusService>();

        return builder;
    }
}
