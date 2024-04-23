namespace Microsoft.Extensions.DependencyInjection;

public interface IAquaBuilder
{
    IServiceCollection Services { get; }
    IConfiguration? Configuration { get; }
}
