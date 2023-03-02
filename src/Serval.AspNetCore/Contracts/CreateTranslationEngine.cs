namespace Serval.AspNetCore.Contracts;

public record CreateTranslationEngine
{
    public string Name { get; init; } = default!;
    public string SourceLanguageTag { get; init; } = default!;
    public string TargetLanguageTag { get; init; } = default!;
    public string Type { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
