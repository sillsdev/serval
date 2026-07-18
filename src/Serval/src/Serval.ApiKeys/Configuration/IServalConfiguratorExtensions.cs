namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddApiKeys(this IServalConfigurator configurator)
    {
        configurator.Services.AddScoped<IApiKeyService, ApiKeyService>();
        configurator.Services.AddScoped<DtoMapper>();

        configurator.AddApiKeysDataAccess();

        configurator.AddHandlers(Assembly.GetExecutingAssembly());

        return configurator;
    }

    public static IServalConfigurator AddApiKeysDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<ApiKey>(
            "api_keys.keys",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<ApiKey>(Builders<ApiKey>.IndexKeys.Ascending(k => k.Owner))
                    ),
            ]
        );

        return configurator;
    }
}
