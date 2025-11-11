namespace Serval.Translation.Contracts;

public record ExecutionDataDto
{
    public int TrainCount { get; init; }
    public int PretranslateCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? EngineSourceLanguageTag { get; init; }
    public string? EngineTargetLanguageTag { get; init; }
    public string? ResolvedSourceLanguage { get; init; }
    public string? ResolvedTargetLanguage { get; init; }
}
