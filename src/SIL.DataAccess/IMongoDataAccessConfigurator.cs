namespace SIL.DataAccess;

public interface IMongoDataAccessConfigurator
{
    IServiceCollection Services { get; }
    IMongoDatabase Database { get; }
}
