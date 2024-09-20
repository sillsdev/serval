namespace Serval.Translation.Contracts;

public record TranslationBuildDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required int Revision { get; init; }
    public string? Name { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public IReadOnlyList<CorpusFilterDto>? TrainOn { get; init; }
    public IReadOnlyList<PretranslateCorpusDto>? Pretranslate { get; init; }
    public required int Step { get; init; }
    public double? PercentCompleted { get; init; }
    public string? Message { get; init; }

    public int? QueueDepth { get; init; }

    /// <summary>
    /// The current build job state.
    /// </summary>
    public required BuildState State { get; init; }
    public DateTime? DateFinished { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
}
