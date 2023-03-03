namespace SIL.DataAccess;

public class MongoDataAccessBuilder : IMongoDataAccessBuilder
{
    public MongoDataAccessBuilder(IServiceCollection services, IMongoDatabase database)
    {
        Services = services;
        Database = database;
    }

    public IServiceCollection Services { get; }
    public IMongoDatabase Database { get; }
}
