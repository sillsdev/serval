namespace Serval.Translation.Contracts;

public record LanguageInfoDto
{
    public required string EngineType { get; init; }
    public required bool IsNative { get; init; }
    public string? InternalCode { get; init; }
}
