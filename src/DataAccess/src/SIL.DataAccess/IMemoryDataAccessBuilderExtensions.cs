namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessBuilderExtensions
{
    public static IMemoryDataAccessBuilder AddRepository<T>(this IMemoryDataAccessBuilder configurator)
        where T : IEntity
    {
        configurator.Services.AddSingleton<IRepository<T>, MemoryRepository<T>>();
        return configurator;
    }
}
