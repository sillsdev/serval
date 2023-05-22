namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServalBuilder AddServal(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddTransient<IFileSystem, FileSystem>();
        return new ServalBuilder(services, configuration);
    }
}
