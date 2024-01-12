namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddDataFileOptions(
        this IServalBuilder builder,
        Action<DataFileOptions> configureOptions
    )
    {
        builder.Services.Configure(configureOptions);
        return builder;
    }

    public static IServalBuilder AddDataFileOptions(this IServalBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<DataFileOptions>(config);
        return builder;
    }

    public static IServalBuilder AddApiOptions(this IServalBuilder builder, Action<ApiOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        return builder;
    }

    public static IServalBuilder AddApiOptions(this IServalBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<ApiOptions>(config);
        return builder;
    }

    public static IServalBuilder AddMemoryDataAccess(
        this IServalBuilder builder,
        Action<IMemoryDataAccessConfigurator> configure
    )
    {
        builder.Services.AddMemoryDataAccess(configure);
        return builder;
    }

    public static IServalBuilder AddMongoDataAccess(
        this IServalBuilder builder,
        Action<IMongoDataAccessConfigurator> configure
    )
    {
        var mongoConnectionString =
            builder.Configuration?.GetConnectionString("Mongo")
            ?? throw new Exception("Mongo connection string not configured");
        builder.Services.AddMongoDataAccess(mongoConnectionString, "Serval", configure);
        builder.Services.AddHealthChecks().AddMongoDb(mongoConnectionString, name: "Mongo");
        return builder;
    }
}
