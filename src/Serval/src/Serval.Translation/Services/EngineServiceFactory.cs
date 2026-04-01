using Microsoft.Extensions.DependencyInjection;

namespace Serval.Translation.Services;

public class EngineServiceFactory(IServiceProvider serviceProvider) : IEngineServiceFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public ITranslationEngineService GetEngineService(string engineType)
    {
        ITranslationEngineService? engine = _serviceProvider.GetKeyedService<ITranslationEngineService>(
            engineType.ToLowerInvariant()
        );
        if (engine is null)
        {
            throw new InvalidOperationException($"No engine registered for type '{engineType}'.");
        }
        return engine;
    }

    public bool TryGetEngineService(string engineType, [NotNullWhen(true)] out ITranslationEngineService? service)
    {
        ITranslationEngineService? engine = _serviceProvider.GetKeyedService<ITranslationEngineService>(
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
