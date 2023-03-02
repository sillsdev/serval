namespace Microsoft.Extensions.DependencyInjection;

internal class ServalConfigurator : IServalConfigurator
{
    public ServalConfigurator(IServiceCollection services, IConfiguration? configuration)
    {
        Services = services;
        Configuration = configuration;
    }

    public IServiceCollection Services { get; }
    public IConfiguration? Configuration { get; }
}
