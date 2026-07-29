namespace Serval.Machine.Shared.Services;

public interface IClearMLQueueService<TEngine>
    where TEngine : ITrainingEngine
{
    public int GetQueueSize(EngineType engineType);
}
