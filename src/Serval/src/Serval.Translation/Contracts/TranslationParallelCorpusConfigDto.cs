namespace Serval.Translation.Contracts;

public record TranslationParallelCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required IReadOnlyList<string> SourceCorpusIds { get; init; } = new List<string>();
    public required IReadOnlyList<string> TargetCorpusIds { get; init; } = new List<string>();
}
