namespace Serval.Machine.Shared.Services;

public interface IClearMLQueueService
{
    public int GetQueueSize(EngineType engineType);
}
