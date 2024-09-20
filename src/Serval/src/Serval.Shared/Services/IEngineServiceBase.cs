namespace Serval.Shared.Services;

public interface IEngineServiceBase
{
    Task RemoveDataFileFromAllCorporaAsync(string dataFileId, CancellationToken cancellationToken = default);
}
