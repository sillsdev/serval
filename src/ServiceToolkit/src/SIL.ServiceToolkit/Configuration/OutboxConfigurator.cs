namespace SIL.ServiceToolkit.Configuration;

internal class OutboxConfigurator(IServiceCollection services) : IOutboxConfigurator
{
    public IServiceCollection Services { get; } = services;
}
