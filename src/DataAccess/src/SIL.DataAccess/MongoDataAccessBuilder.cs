namespace Microsoft.Extensions.DependencyInjection;

public class MongoDataAccessBuilder(IServiceCollection services) : IMongoDataAccessBuilder
{
    public IServiceCollection Services { get; } = services;
}
