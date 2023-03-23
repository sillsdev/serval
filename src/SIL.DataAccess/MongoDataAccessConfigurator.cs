namespace Microsoft.Extensions.DependencyInjection;

public class MongoDataAccessConfigurator : IMongoDataAccessConfigurator
{
    public MongoDataAccessConfigurator(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}
