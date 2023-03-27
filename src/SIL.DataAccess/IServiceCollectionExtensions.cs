namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryDataAccess(
        this IServiceCollection services,
        Action<IMemoryDataAccessConfigurator> configure
    )
    {
        services.AddTransient<SIL.DataAccess.IIdGenerator, ObjectIdGenerator>();
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

        services.Configure<MongoDataAccessOptions>(options => options.Url = new MongoUrl(connectionString));
        services.AddTransient<SIL.DataAccess.IIdGenerator, ObjectIdGenerator>();
        services.AddSingleton<IMongoClient>(
            sp => new MongoClient(sp.GetRequiredService<IOptions<MongoDataAccessOptions>>().Value.Url)
        );
        services.AddSingleton(
            sp =>
                sp.GetRequiredService<IMongoClient>()
                    .GetDatabase(sp.GetRequiredService<IOptions<MongoDataAccessOptions>>().Value.Url.DatabaseName)
        );
        services.TryAddScoped<IMongoDataAccessContext, MongoDataAccessContext>();
        services.AddScoped<IDataAccessContext>(sp => sp.GetRequiredService<IMongoDataAccessContext>());
        services.AddHostedService<MongoDataAccessInitializeService>();
        configure(new MongoDataAccessConfigurator(services));
        return services;
    }
}
