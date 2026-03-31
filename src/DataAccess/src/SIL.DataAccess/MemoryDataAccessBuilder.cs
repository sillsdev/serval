namespace Microsoft.Extensions.DependencyInjection;

public class MemoryDataAccessBuilder(IServiceCollection services) : IMemoryDataAccessBuilder
{
    public IServiceCollection Services { get; } = services;
}
