namespace SIL.DataAccess;

public interface IMongoDataAccessContext : IDataAccessContext
{
    IClientSessionHandle? Session { get; }
}
