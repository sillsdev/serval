namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IAquaBuilder AddAqua(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (!Sldr.IsInitialized)
            Sldr.Initialize();

        services.AddScoped<ICorpusService, CorpusService>();
        services.AddScoped<IJobService, JobService>();

        AquaBuilder builder = new(services, configuration);
        if (configuration is null)
        {
            builder.AddAquaOptions(o => { });
        }
        else
        {
            builder.AddAquaOptions(configuration.GetSection(AquaOptions.Key));
        }
        return builder;
    }
}
