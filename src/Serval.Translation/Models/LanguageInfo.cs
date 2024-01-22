namespace Serval.Translation.Models;

public class LanguageInfo
{
    public string InternalCode { get; set; } = default!;
    public bool IsNative { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string EngineType { get; set; } = default!;
}
