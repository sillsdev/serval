namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServalBuilder AddServal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IFileSystem, FileSystem>();
        services.AddTransient<IScriptureDataFileService, ScriptureDataFileService>();
        return new ServalBuilder(services, configuration);
    }
}
