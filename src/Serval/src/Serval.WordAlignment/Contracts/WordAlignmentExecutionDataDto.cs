namespace Serval.WordAlignment.Contracts;

public record WordAlignmentExecutionDataDto
{
    public int TrainCount { get; init; }
    public int WordAlignCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? EngineSourceLanguageTag { get; init; }
    public string? EngineTargetLanguageTag { get; init; }
}
