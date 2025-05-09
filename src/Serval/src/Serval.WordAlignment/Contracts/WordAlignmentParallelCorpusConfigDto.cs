namespace Serval.WordAlignment.Contracts;

public record WordAlignmentParallelCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required IReadOnlyList<string> SourceCorpusIds { get; init; }
    public required IReadOnlyList<string> TargetCorpusIds { get; init; }
}
