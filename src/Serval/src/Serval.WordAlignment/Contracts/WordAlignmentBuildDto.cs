namespace Serval.WordAlignment.Contracts;

public record WordAlignmentBuildDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required int Revision { get; init; }
    public string? Name { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public IReadOnlyList<TrainingCorpusDto>? TrainOn { get; init; }
    public IReadOnlyList<WordAlignmentCorpusDto>? WordAlignOn { get; init; }
    public required int Step { get; init; }
    public double? PercentCompleted { get; init; }
    public string? Message { get; init; }

    public int? QueueDepth { get; init; }

    /// <summary>
    /// The current build job state.
    /// </summary>
    public required JobState State { get; init; }
    public DateTime? DateFinished { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
    public string? DeploymentVersion { get; init; }
    public IReadOnlyDictionary<string, string>? ExecutionData { get; init; }
}
