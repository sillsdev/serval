namespace SIL.DataAccess;

public interface IMongoDataAccessBuilder
{
    IServiceCollection Services { get; }
    IMongoDatabase Database { get; }
}
