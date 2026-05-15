namespace Microsoft.Extensions.DependencyInjection;

internal class ServalConfigurator(
    IServiceCollection services,
    IConfiguration configuration,
    IMongoDataAccessBuilder dataAccess
) : IServalConfigurator
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = configuration;
    public IMongoDataAccessBuilder DataAccess { get; } = dataAccess;
    public ICollection<string> JobQueues { get; } = [];

    public IServalConfigurator AddStartupTask(Func<IServiceProvider, CancellationToken, Task> startupTask)
    {
        Services.AddStartupTask(startupTask);
        return this;
    }
}
