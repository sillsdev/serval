namespace SIL.DataAccess;

public class MongoDataAccessOptions
{
    public IList<Func<IMongoDatabase, Task>> Initializers { get; } = new List<Func<IMongoDatabase, Task>>();
}
