using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public interface IServalBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
    IMongoDataAccessBuilder DataAccess { get; }
    ICollection<string> JobQueues { get; }
}
