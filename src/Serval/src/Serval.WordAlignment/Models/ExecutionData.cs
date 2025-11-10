namespace Serval.WordAlignment.Models;

public record ExecutionData
{
    public int? TrainCount { get; init; }
    public int? WordAlignCount { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public string? EngineSourceLanguageTag { get; init; }
    public string? EngineTargetLanguageTag { get; init; }
}
