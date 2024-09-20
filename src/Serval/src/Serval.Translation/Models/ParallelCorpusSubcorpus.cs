namespace Serval.Translation.Models;

public record ParallelCorpusSubcorpus
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Language { get; set; }
    public required IReadOnlyList<CorpusFile> Files { get; set; }
}
