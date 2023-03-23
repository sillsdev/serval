namespace SIL.DataAccess;

public class MongoDataAccessInitializeService : IHostedService
{
    private readonly IMongoDatabase _database;
    private readonly IOptions<MongoDataAccessOptions> _options;

    public MongoDataAccessInitializeService(IMongoDatabase database, IOptions<MongoDataAccessOptions> options)
    {
        _database = database;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (Func<IMongoDatabase, Task> initializer in _options.Value.Initializers)
            await initializer(_database);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
