namespace Serval.Translation.Services;

public static class ServicesExtensions
{
    public static ITranslationEngineService GetEngineService(this IEngineServiceFactory factory, string engineType)
    {
        if (factory.TryGetEngineService(engineType, out ITranslationEngineService? service))
            return service;
        throw new InvalidOperationException($"No engine registered for type '{engineType}'.");
    }

    public static bool EngineTypeExists(this IEngineServiceFactory factory, string engineType)
    {
        return factory.TryGetEngineService(engineType, out _);
    }
}
