namespace Serval.AspNetCore.Contracts;

public record TranslateResult
{
    public List<TranslationResult> Results { get; init; } = default!;
}
