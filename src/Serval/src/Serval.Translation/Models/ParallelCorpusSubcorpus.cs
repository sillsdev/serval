namespace Serval.Translation.Models;

public record ParallelCorpusSubcorpus //TODO Better name? Monolingual corpus?
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public required string Language { get; set; }
    public required IReadOnlyList<CorpusFile> Files { get; set; }
}
