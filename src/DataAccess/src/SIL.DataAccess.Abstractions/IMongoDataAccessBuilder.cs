namespace Microsoft.Extensions.DependencyInjection;

public interface IMongoDataAccessBuilder
{
    IServiceCollection Services { get; }
}
