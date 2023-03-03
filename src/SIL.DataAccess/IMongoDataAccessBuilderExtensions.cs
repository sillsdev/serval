namespace SIL.DataAccess;

public static class IMongoDataAccessBuilderExtensions
{
    public static IMongoDataAccessBuilder AddMongoRepository<T>(
        this IMongoDataAccessBuilder builder,
        string collectionName,
        Action<BsonClassMap<T>>? mapSetup = null,
        Action<IMongoCollection<T>>? init = null
    )
        where T : IEntity
    {
        DataAccessClassMap.RegisterClass<T>(cm => mapSetup?.Invoke(cm));

        IMongoCollection<T> collection = builder.Database.GetCollection<T>(collectionName);
        if (init is not null)
            init(collection);
        builder.Services.AddTransient<IRepository<T>>(sp => CreateMongoRepository(collection));
        return builder;
    }

    private static MongoRepository<T> CreateMongoRepository<T>(IMongoCollection<T> collection)
        where T : IEntity
    {
        return new MongoRepository<T>(collection);
    }
}
