namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddDataFiles(this IServalBuilder builder)
    {
        if (builder.Configuration is null)
            throw new InvalidOperationException("Configuration is required");
        else
            builder.AddDataFileOptions(builder.Configuration.GetSection(DataFileOptions.Key));

        builder.Services.AddScoped<IDataFileService, DataFileService>();
        builder.Services.AddHostedService<DeletedFileCleaner>();

        builder.Services.AddScoped<ICorpusService, CorpusService>();

        return builder;
    }
}
