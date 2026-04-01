namespace Serval.Translation.Contracts;

public record LanguageInfoContract
{
    public required bool IsNative { get; set; }
    public string? InternalCode { get; set; }
}
