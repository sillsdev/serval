namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddDataFiles(this IServalConfigurator configurator)
    {
        if (configurator.Configuration is null)
            configurator.AddDataFileOptions(o => { });
        else
            configurator.AddDataFileOptions(configurator.Configuration.GetSection(DataFileOptions.Key));

        configurator.Services.AddAutoMapper(Assembly.GetExecutingAssembly());

        configurator.Services.AddScoped<IDataFileService, DataFileService>();
        return configurator;
    }
}
