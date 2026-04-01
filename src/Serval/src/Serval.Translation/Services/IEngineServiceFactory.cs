namespace Serval.Translation.Services;

public interface IEngineServiceFactory
{
    bool TryGetEngineService(string engineType, [NotNullWhen(true)] out ITranslationEngineService? service);
}
