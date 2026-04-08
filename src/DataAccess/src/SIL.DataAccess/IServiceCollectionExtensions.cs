namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IMemoryDataAccessBuilder AddMemoryDataAccess(this IServiceCollection services)
    {
        services.TryAddTransient<SIL.DataAccess.IIdGenerator, ObjectIdGenerator>();
        services.TryAddScoped<IDataAccessContext, MemoryDataAccessContext>();
        return new MemoryDataAccessBuilder(services);
    }

    public static IMongoDataAccessBuilder AddMongoDataAccess(
        this IServiceCollection services,
        string connectionString,
        string entityNamespace
    )
    {
        DataAccessClassMap.RegisterConventions(
            entityNamespace,
            new StringIdStoredAsObjectIdConvention(),
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true),
            new IgnoreIfNullConvention(true),
            new ObjectRefConvention()
        );

        // Configure conventions for schema_versions
        DataAccessClassMap.RegisterConventions(
            "SIL.DataAccess.Models",
            new StringIdStoredAsObjectIdConvention(),
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true),
            new IgnoreIfNullConvention(true),
            new ObjectRefConvention()
        );

        services.Configure<MongoDataAccessOptions>(options => options.Url = new MongoUrl(connectionString));
        services.TryAddTransient<SIL.DataAccess.IIdGenerator, ObjectIdGenerator>();
        services.TryAddSingleton<IMongoClient>(sp =>
        {
            var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
            clientSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
            return new MongoClient(clientSettings);
        });
        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>()
                .GetDatabase(sp.GetRequiredService<IOptions<MongoDataAccessOptions>>().Value.Url.DatabaseName)
        );
        services.TryAddScoped<IMongoDataAccessContext, MongoDataAccessContext>();
        services.TryAddScoped<IDataAccessContext>(sp => sp.GetRequiredService<IMongoDataAccessContext>());
        services.AddHostedService<MongoDataAccessInitializeService>();
        MongoDataAccessBuilder builder = new(services);

        // Configure the schema_versions repository
        builder.AddRepository<SchemaVersion>(
            "schema_versions",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<SchemaVersion>(
                            Builders<SchemaVersion>.IndexKeys.Ascending(p => p.Collection)
                        )
                    ),
            ]
        );

        return builder;
    }
}
