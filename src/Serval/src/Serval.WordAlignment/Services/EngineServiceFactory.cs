namespace Serval.WordAlignment.Services;

public class EngineServiceFactory(IServiceProvider serviceProvider) : IEngineServiceFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public bool TryGetEngineService(string engineType, [NotNullWhen(true)] out IWordAlignmentEngineService? service)
    {
        // We convert to pascal case first to correctly handle kebab case
        IWordAlignmentEngineService? engine = _serviceProvider.GetKeyedService<IWordAlignmentEngineService>(
            engineType.ToPascalCase().ToLowerInvariant()
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
