namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryDataAccess(
        this IServiceCollection services,
        Action<IMemoryDataAccessConfigurator> configure
    )
    {
        services.AddScoped<IDataAccessContext, MemoryDataAccessContext>();
        configure(new MemoryDataAccessConfigurator(services));
        return services;
    }

    public static IServiceCollection AddMongoDataAccess(
        this IServiceCollection services,
        string connectionString,
        string entityNamespace,
        Action<IMongoDataAccessConfigurator> configure
    )
    {
        DataAccessClassMap.RegisterConventions(
            entityNamespace,
            new StringIdStoredAsObjectIdConvention(),
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreIfNullConvention(true),
            new ObjectRefConvention()
        );

        var mongoUrl = new MongoUrl(connectionString);
        var mongoClient = new MongoClient(mongoUrl);
        var database = mongoClient.GetDatabase(mongoUrl.DatabaseName);
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton(database);
        services.TryAddScoped<IMongoDataAccessContext, MongoDataAccessContext>();
        services.AddScoped<IDataAccessContext>(sp => sp.GetRequiredService<IMongoDataAccessContext>());
        configure(new MongoDataAccessConfigurator(services, database));
        return services;
    }
}
