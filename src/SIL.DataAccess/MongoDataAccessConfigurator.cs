namespace SIL.DataAccess;

public class MongoDataAccessConfigurator : IMongoDataAccessConfigurator
{
    public MongoDataAccessConfigurator(IServiceCollection services, IMongoDatabase database)
    {
        Services = services;
        Database = database;
    }

    public IServiceCollection Services { get; }
    public IMongoDatabase Database { get; }
}
