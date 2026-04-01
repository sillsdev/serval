namespace Serval.WordAlignment.Services;

public interface IEngineServiceFactory
{
    bool TryGetEngineService(string engineType, [NotNullWhen(true)] out IWordAlignmentEngineService? service);
}
