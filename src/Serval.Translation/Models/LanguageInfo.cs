namespace Serval.Translation.Models;

public class LanguageInfo
{
    public string ResolvedLanguageCode { get; set; } = default!;
    public bool NativeLanguageSupport { get; set; } = default!;
    public string CommonLanguageName { get; set; } = default!;
    public string EngineType { get; set; } = default!;
}
