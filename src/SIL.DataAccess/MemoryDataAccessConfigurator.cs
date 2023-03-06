namespace SIL.DataAccess;

public class MemoryDataAccessConfigurator : IMemoryDataAccessConfigurator
{
    public MemoryDataAccessConfigurator(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}
