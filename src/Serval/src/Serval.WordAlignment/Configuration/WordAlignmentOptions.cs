namespace Serval.WordAlignment.Configuration;

public class WordAlignmentOptions
{
    public const string Key = "WordAlignment";

    public List<EngineInfo> Engines { get; set; } = new List<EngineInfo>();
}

public class EngineInfo
{
    public string Type { get; set; } = "";
    public string Address { get; set; } = "";
}
