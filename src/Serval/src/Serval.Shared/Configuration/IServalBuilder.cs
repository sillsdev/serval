using Microsoft.Extensions.DependencyInjection;

namespace Serval.Shared.Configuration;

public interface IServalBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
}
