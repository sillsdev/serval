namespace Serval.Shared.Models;

public record TrainingCorpus
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }
    public required IReadOnlyList<CorpusFile> SourceFiles { get; set; }
    public required IReadOnlyList<CorpusFile> TargetFiles { get; set; }
}
