namespace SIL.DataAccess;

public class MongoDataAccessOptions
{
    public MongoUrl Url { get; set; } = new MongoUrl("mongodb://localhost:27017");
    public IList<Func<IMongoDatabase, Task>> Initializers { get; } = new List<Func<IMongoDatabase, Task>>();
}
