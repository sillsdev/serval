namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddDataFileOptions(
        this IServalConfigurator configurator,
        Action<DataFileOptions> configureOptions
    )
    {
        configurator.Services.Configure(configureOptions);
        return configurator;
    }

    public static IServalConfigurator AddDataFileOptions(this IServalConfigurator configurator, IConfiguration config)
    {
        configurator.Services.Configure<DataFileOptions>(config);
        return configurator;
    }

    public static IServalConfigurator AddApiOptions(
        this IServalConfigurator configurator,
        Action<ApiOptions> configureOptions
    )
    {
        configurator.Services.Configure(configureOptions);
        return configurator;
    }

    public static IServalConfigurator AddApiOptions(this IServalConfigurator configurator, IConfiguration config)
    {
        configurator.Services.Configure<ApiOptions>(config);
        return configurator;
    }

    public static IServalConfigurator AddMemoryDataAccess(
        this IServalConfigurator configurator,
        Action<IMemoryDataAccessConfigurator> configure
    )
    {
        configurator.Services.AddMemoryDataAccess(configure);
        return configurator;
    }

    public static IServalConfigurator AddMongoDataAccess(
        this IServalConfigurator configurator,
        string connectionString,
        Action<IMongoDataAccessConfigurator> configure
    )
    {
        configurator.Services.AddMongoDataAccess(connectionString, "Serval", configure);
        return configurator;
    }
}
