namespace Microsoft.Extensions.DependencyInjection;

internal class AquaBuilder(IServiceCollection services, IConfiguration? configuration) : IAquaBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration? Configuration { get; } = configuration;
}
