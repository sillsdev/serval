namespace SIL.DataAccess;

public class MongoDataAccessInitializeService(
    IServiceProvider provider,
    IMongoDatabase database,
    IOptions<MongoDataAccessOptions> options
) : IHostedService
{
    private readonly IMongoClient _client = client;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (Func<IServiceProvider, IMongoDatabase, Task> initializer in options.Value.Initializers)
            await initializer(provider, database).ConfigureAwait(false);
        foreach (Func<IMongoClient, Task> initializer in _options.Value.Initializers)
            await initializer(_client).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
