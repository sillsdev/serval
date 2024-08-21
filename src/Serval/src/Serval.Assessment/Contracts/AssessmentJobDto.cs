namespace Serval.Assessment.Contracts;

public record AssessmentJobDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required int Revision { get; init; }
    public string? Name { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }
    public double? PercentCompleted { get; init; }
    public string? Message { get; init; }

    /// <summary>
    /// The current job state.
    /// </summary>
    public required JobState State { get; init; }
    public DateTime? DateFinished { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
}
