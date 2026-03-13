using Microsoft.Extensions.DependencyInjection;

namespace Serval.Shared.Configuration;

public class ServalBuilder(IServiceCollection services, IConfiguration configuration) : IServalBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = configuration;
}
