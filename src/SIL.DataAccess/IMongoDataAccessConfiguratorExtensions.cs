namespace SIL.DataAccess;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddRepository<T>(
        this IMongoDataAccessConfigurator configurator,
        string collectionName,
        Action<BsonClassMap<T>>? mapSetup = null,
        Action<IMongoCollection<T>>? init = null
    )
        where T : IEntity
    {
        DataAccessClassMap.RegisterClass<T>(cm => mapSetup?.Invoke(cm));

        IMongoCollection<T> collection = configurator.Database.GetCollection<T>(collectionName);
        if (init is not null)
            init(collection);
        configurator.Services.AddScoped<IRepository<T>>(
            sp => CreateRepository(sp.GetRequiredService<IMongoDataAccessContext>(), collection)
        );
        return configurator;
    }

    private static MongoRepository<T> CreateRepository<T>(
        IMongoDataAccessContext context,
        IMongoCollection<T> collection
    )
        where T : IEntity
    {
        return new MongoRepository<T>(context, collection);
    }
}
