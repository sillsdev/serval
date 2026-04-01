namespace Serval.Translation.Contracts;

public record LanguageInfo
{
    public required bool IsNative { get; set; }
    public string? InternalCode { get; set; }
}
