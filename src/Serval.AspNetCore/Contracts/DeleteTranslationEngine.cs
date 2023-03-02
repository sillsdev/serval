namespace Serval.AspNetCore.Contracts;

public record DeleteTranslationEngine
{
    public string EngineId { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
