namespace Serval.Translation.Models;

public class LanguageInfo
{
    public string InternalCode { get; set; } = default!;
    public bool IsSupportedNatively { get; set; } = default!;
    public string CommonLanguageName { get; set; } = default!;
    public string EngineType { get; set; } = default!;
}
