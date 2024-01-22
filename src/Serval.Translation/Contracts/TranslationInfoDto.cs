namespace Serval.Translation.Contracts;

public class LanguageInfoDto
{
    public string InternalCode { get; set; } = default!;
    public bool IsSupportedNatively { get; set; } = default!;
    public string CommonLanguageName { get; set; } = default!;
    public string EngineType { get; set; } = default!;
}
