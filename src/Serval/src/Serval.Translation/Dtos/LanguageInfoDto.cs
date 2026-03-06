namespace Serval.Translation.Dtos;

public record LanguageInfoDto
{
    public required string EngineType { get; init; }
    public required bool IsNative { get; init; }
    public string? InternalCode { get; init; }
}
