namespace Serval.Machine.Shared.Services;

public interface IModelFactory
{
    void InitNew(string engineDir);
    void Cleanup(string engineDir);
    Task UpdateEngineFromAsync(string engineDir, Stream source, CancellationToken cancellationToken = default);
}
