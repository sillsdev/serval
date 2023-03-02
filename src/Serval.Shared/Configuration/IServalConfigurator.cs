namespace Microsoft.Extensions.DependencyInjection;

public interface IServalConfigurator
{
    IServiceCollection Services { get; }
    IConfiguration? Configuration { get; }
}
