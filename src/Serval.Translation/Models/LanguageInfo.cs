namespace Serval.Translation.Models;

public record LanguageInfo
{
    public required string EngineType { get; set; }
    public required bool IsNative { get; set; }
    public string? InternalCode { get; set; }
}
