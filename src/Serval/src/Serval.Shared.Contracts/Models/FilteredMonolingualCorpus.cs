namespace Serval.Shared.Models;

public record FilteredMonolingualCorpus
{
    public required string Id { get; set; }
    public required string Language { get; set; }
    public required IReadOnlyList<ResolvedCorpusFile> Files { get; set; }
    public HashSet<string>? TrainOnTextIds { get; set; }
    public Dictionary<string, HashSet<int>>? TrainOnChapters { get; set; }
    public HashSet<string>? InferenceTextIds { get; set; }
    public Dictionary<string, HashSet<int>>? InferenceChapters { get; set; }
    public bool TrainOnAll { get; set; }
    public bool PretranslateAll { get; set; }

    public bool IsFiltered =>
        TrainOnTextIds != null || TrainOnChapters != null || InferenceTextIds != null || InferenceChapters != null;
}
