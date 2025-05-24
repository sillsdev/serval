namespace Microsoft.Extensions.DependencyInjection;

public interface IOutboxConfigurator
{
    IServiceCollection Services { get; }
}
