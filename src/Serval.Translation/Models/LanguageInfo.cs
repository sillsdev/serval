namespace Serval.Translation.Models;

public class LanguageInfo
{
    public string EngineType = default!;
    public bool IsNative { get; set; } = default!;
    public string? InternalCode { get; set; } = default!;
    public string? Name { get; set; } = default!;
}
