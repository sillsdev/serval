namespace Microsoft.Extensions.DependencyInjection;

public class MemoryDataAccessConfigurator : IMemoryDataAccessConfigurator
{
    public MemoryDataAccessConfigurator(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}
