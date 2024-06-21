namespace Microsoft.Extensions.DependencyInjection;

public class MemoryDataAccessConfigurator(IServiceCollection services) : IMemoryDataAccessConfigurator
{
    public IServiceCollection Services { get; } = services;
}
