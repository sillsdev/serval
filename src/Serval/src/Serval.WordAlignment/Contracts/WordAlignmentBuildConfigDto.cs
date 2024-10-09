namespace Serval.WordAlignment.Contracts;

public record WordAlignmentBuildConfigDto
{
    public string? Name { get; init; }
    public IReadOnlyList<TrainingCorpusConfigDto>? TrainOn { get; init; }
    public IReadOnlyList<WordAlignOnCorpusConfigDto>? WordAlignOn { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
}
