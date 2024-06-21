namespace Microsoft.Extensions.DependencyInjection;

public class MongoDataAccessConfigurator(IServiceCollection services) : IMongoDataAccessConfigurator
{
    public IServiceCollection Services { get; } = services;
}
