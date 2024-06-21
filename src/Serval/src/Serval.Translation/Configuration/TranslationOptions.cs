namespace Serval.Translation.Configuration;

public class TranslationOptions
{
    public const string Key = "Translation";

    public List<EngineInfo> Engines { get; set; } = new List<EngineInfo>();
}

public class EngineInfo
{
    public string Type { get; set; } = "";
    public string Address { get; set; } = "";
}
