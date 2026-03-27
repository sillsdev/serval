namespace Serval.WordAlignment.Services;

public interface IEngineServiceFactory
{
    IWordAlignmentEngineService GetEngineService(string engineType);
    bool TryGetEngineService(string engineType, [NotNullWhen(true)] out IWordAlignmentEngineService? service);
}
