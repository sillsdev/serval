namespace Serval.Machine.Shared.Models;

public interface ITrainingEngine : IEntity
{
    public string EngineId { get; init; }
    public EngineType Type { get; init; }
    public string SourceLanguage { get; init; }
    public string TargetLanguage { get; init; }
    public int BuildRevision { get; init; }
    public Build? CurrentBuild { get; init; }
}
