namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddRepository<T>(
        this IMongoDataAccessConfigurator configurator,
        string collectionName,
        Action<BsonClassMap<T>>? mapSetup = null,
        Func<IMongoCollection<T>, Task>? init = null
    )
        where T : IEntity
    {
        DataAccessClassMap.RegisterClass<T>(cm => mapSetup?.Invoke(cm));

        if (init is not null)
        {
            configurator.Services.Configure<MongoDataAccessOptions>(options =>
            {
                options.Initializers.Add(database => init(database.GetCollection<T>(collectionName)));
            });
        }

        configurator.Services.AddScoped<IRepository<T>>(sp =>
            CreateRepository(
                sp.GetRequiredService<IMongoDataAccessContext>(),
                sp.GetRequiredService<IMongoDatabase>().GetCollection<T>(collectionName)
            )
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
