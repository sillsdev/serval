namespace Serval.Machine.Shared.Configuration;

public class ClearMLBuildQueue
{
    public EngineType TranslationEngineType { get; set; }
    public string ModelType { get; set; } = "";
    public string Queue { get; set; } = "default";
    public string DockerImage { get; set; } = "";
}
