namespace Serval.Translation.Models;

public class LanguageInfo
{
    public string EngineType { get; set; } = default!;
    public bool IsNative { get; set; } = default!;
    public string? InternalCode { get; set; } = default!;
}
