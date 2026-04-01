namespace Microsoft.Extensions.DependencyInjection;

internal class ServalBuilder(
    IServiceCollection services,
    IConfiguration configuration,
    IMongoDataAccessBuilder dataAccess
) : IServalBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = configuration;
    public IMongoDataAccessBuilder DataAccess { get; } = dataAccess;
    public ICollection<string> JobQueues { get; } = [];
}
