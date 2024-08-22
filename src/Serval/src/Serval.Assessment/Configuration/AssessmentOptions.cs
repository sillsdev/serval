namespace Serval.Assessment.Configuration;

public class AssessmentOptions
{
    public const string Key = "Assessment";

    public List<EngineInfo> Engines { get; set; } = new List<EngineInfo>();
}

public class EngineInfo
{
    public string Type { get; set; } = "";
    public string Address { get; set; } = "";
}
