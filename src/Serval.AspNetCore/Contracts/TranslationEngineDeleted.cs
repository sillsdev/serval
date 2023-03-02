namespace Serval.AspNetCore.Contracts;

public record TranslationEngineDeleted
{
    public string EngineId { get; init; } = default!;
}
