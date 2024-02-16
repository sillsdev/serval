namespace SIL.DataAccess;

public class MongoDataAccessInitializeService(IMongoDatabase database, IOptions<MongoDataAccessOptions> options)
    : IHostedService
{
    private readonly IMongoDatabase _database = database;
    private readonly IOptions<MongoDataAccessOptions> _options = options;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (Func<IMongoDatabase, Task> initializer in _options.Value.Initializers)
            await initializer(_database).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
