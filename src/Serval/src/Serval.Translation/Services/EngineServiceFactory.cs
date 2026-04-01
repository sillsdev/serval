namespace Serval.Translation.Services;

public class EngineServiceFactory(IServiceProvider serviceProvider) : IEngineServiceFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

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
