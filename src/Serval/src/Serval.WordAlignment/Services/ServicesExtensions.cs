namespace Serval.WordAlignment.Services;

public static class ServicesExtensions
{
    public static IWordAlignmentEngineService GetEngineService(this IEngineServiceFactory factory, string engineType)
    {
        if (factory.TryGetEngineService(engineType, out IWordAlignmentEngineService? service))
            return service;
        throw new InvalidOperationException($"No engine registered for type '{engineType}'.");
    }

    public static bool EngineTypeExists(this IEngineServiceFactory factory, string engineType)
    {
        return factory.TryGetEngineService(engineType, out _);
    }
}
