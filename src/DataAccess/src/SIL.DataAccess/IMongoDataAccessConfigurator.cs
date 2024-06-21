namespace Microsoft.Extensions.DependencyInjection;

public interface IMongoDataAccessConfigurator
{
    IServiceCollection Services { get; }
}
