namespace Microsoft.Extensions.DependencyInjection;

public interface IMemoryDataAccessConfigurator
{
    IServiceCollection Services { get; }
}
