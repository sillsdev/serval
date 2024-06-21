namespace Microsoft.Extensions.DependencyInjection;

internal class ServalBuilder(IServiceCollection services, IConfiguration? configuration) : IServalBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration? Configuration { get; } = configuration;
}
