namespace Serval.Translation.Contracts;

public class LanguageInfoDto
{
    public string EngineType { get; set; } = default!;
    public bool IsNative { get; set; } = default!;
    public string? InternalCode { get; set; } = default!;
}
