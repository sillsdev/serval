namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryRepository<T>(this IServiceCollection services)
        where T : IEntity
    {
        services.AddSingleton<IRepository<T>, MemoryRepository<T>>();
        return services;
    }

    public static IServiceCollection AddMongoDataAccess(
        this IServiceCollection services,
        string connectionString,
        Action<IMongoDataAccessBuilder> configure
    )
    {
        DataAccessClassMap.RegisterConventions(
            "SIL.DataAccess",
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
        configure(new MongoDataAccessBuilder(services, database));
        return services;
    }
}
