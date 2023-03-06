namespace SIL.DataAccess;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddRepository<T>(this IMemoryDataAccessConfigurator configurator)
        where T : IEntity
    {
        configurator.Services.AddSingleton<IRepository<T>, MemoryRepository<T>>();
        return configurator;
    }
}
