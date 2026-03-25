namespace Microsoft.Extensions.DependencyInjection;

public class MachineBuilder(IServiceCollection services, IConfiguration configuration) : IMachineBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = configuration;
}
