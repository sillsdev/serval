namespace Microsoft.Extensions.DependencyInjection;

public class ServalBuilder(IServiceCollection services, IConfiguration configuration) : IServalBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = configuration;
}
