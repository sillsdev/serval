namespace SIL.DataAccess;

public class MongoDataAccessInitializeService(
    IServiceProvider provider,
    IMongoDatabase database,
    IOptions<MongoDataAccessOptions> options
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (Func<IServiceProvider, IMongoDatabase, Task> initializer in options.Value.Initializers)
            await initializer(provider, database).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
