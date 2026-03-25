namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddRepository<T>(
        this IMongoDataAccessConfigurator configurator,
        string collectionName,
        Action<BsonClassMap<T>>? mapSetup = null,
        IReadOnlyList<Func<IMongoCollection<T>, Task>>? init = null
    )
        where T : IEntity
    {
        DataAccessClassMap.RegisterClass<T>(cm => mapSetup?.Invoke(cm));

        if (init is not null)
        {
            configurator.Services.Configure<MongoDataAccessOptions>(options =>
            {
                options.Initializers.Add(
                    async (serviceProvider, database) =>
                    {
                        using IServiceScope scope = serviceProvider.CreateScope();
                        var schemaVersions = scope.ServiceProvider.GetRequiredService<IRepository<SchemaVersion>>();
                        SchemaVersion? schemaVersion = await schemaVersions.GetAsync(s =>
                            s.Collection == collectionName
                        );
                        int currentVersion = schemaVersion?.Version ?? 0;
                        IMongoCollection<T> collection = database.GetCollection<T>(collectionName);
                        for (int i = currentVersion + 1; i <= init.Count; i++)
                        {
                            await init[i - 1](collection);
                            await schemaVersions.UpdateAsync(
                                s => s.Collection == collectionName,
                                u => u.Set(s => s.Version, i),
                                upsert: true
                            );
                        }
                    }
                );
            });
        }

        configurator.Services.TryAddScoped<IRepository<T>>(sp =>
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
