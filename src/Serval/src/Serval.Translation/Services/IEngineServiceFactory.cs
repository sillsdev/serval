namespace Serval.Translation.Services;

public interface IEngineServiceFactory
{
    ITranslationEngineService GetEngineService(string engineType);
    bool TryGetEngineService(string engineType, [NotNullWhen(true)] out ITranslationEngineService? service);
}
