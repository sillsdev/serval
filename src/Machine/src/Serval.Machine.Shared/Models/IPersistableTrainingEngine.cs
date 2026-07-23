namespace Serval.Machine.Shared.Models;

public interface IPersistableTrainingEngine : ITrainingEngine
{
    bool IsModelPersisted { get; init; }
}
