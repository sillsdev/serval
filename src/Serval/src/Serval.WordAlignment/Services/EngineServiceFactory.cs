using Microsoft.Extensions.DependencyInjection;

namespace Serval.WordAlignment.Services;

public class EngineServiceFactory(IServiceProvider serviceProvider) : IEngineServiceFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public IWordAlignmentEngineService GetEngineService(string engineType)
    {
        IWordAlignmentEngineService? engine = _serviceProvider.GetKeyedService<IWordAlignmentEngineService>(
            engineType.ToLowerInvariant()
        );
        if (engine is null)
        {
            throw new InvalidOperationException($"No engine registered for type '{engineType}'.");
        }
        return engine;
    }

    public bool TryGetEngineService(string engineType, [NotNullWhen(true)] out IWordAlignmentEngineService? service)
    {
        IWordAlignmentEngineService? engine = _serviceProvider.GetKeyedService<IWordAlignmentEngineService>(
            engineType.ToLowerInvariant()
        );
        if (engine is null)
        {
            service = null;
            return false;
        }
        service = engine;
        return true;
    }
}
