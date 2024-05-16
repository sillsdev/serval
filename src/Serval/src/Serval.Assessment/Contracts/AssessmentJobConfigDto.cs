namespace Serval.Assessment.Contracts;

public record AssessmentJobConfigDto
{
    public string? Name { get; init; }
    public IReadOnlyList<string>? TextIds { get; init; }
    public string? ScriptureRange { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
}
