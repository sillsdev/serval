namespace Serval.AspNetCore.Contracts;

public record TranslationEngineNotFound
{
    public string EngineId { get; init; } = default!;
}
