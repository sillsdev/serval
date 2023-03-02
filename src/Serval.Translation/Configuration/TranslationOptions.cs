namespace Serval.Translation.Configuration;

public class TranslationOptions
{
    public const string Key = "Translation";

    public List<Engine> Engines { get; set; } = new List<Engine>();
}

public class Engine
{
    public string Type { get; set; } = "";
    public string Address { get; set; } = "";
}
