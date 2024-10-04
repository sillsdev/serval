namespace Serval.Translation.Models;

public record TranslationEngine : IEngine
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Owner { get; init; }
    public string? Name { get; init; }
    public required string Type { get; init; }
    public bool IsJobRunning { get; init; }
    public int JobRevision { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required IReadOnlyList<TrainingCorpus> Corpora { get; init; }
    public bool? IsModelPersisted { get; init; }
    public double Confidence { get; init; }
    public int CorpusSize { get; init; }
}
