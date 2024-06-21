namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddDataFiles(this IServalBuilder builder)
    {
        if (builder.Configuration is null)
            builder.AddDataFileOptions(o => { });
        else
            builder.AddDataFileOptions(builder.Configuration.GetSection(DataFileOptions.Key));

        builder.Services.AddScoped<IDataFileService, DataFileService>();
        builder.Services.AddHostedService<DeletedFileCleaner>();
        return builder;
    }
}
