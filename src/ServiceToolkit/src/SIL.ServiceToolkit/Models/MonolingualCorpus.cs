namespace SIL.ServiceToolkit.Models;

public record MonolingualCorpus
{
    public required string Id { get; set; }
    public required string Language { get; set; }
    public required IReadOnlyList<CorpusFile> Files { get; set; }
    public HashSet<string>? TrainOnTextIds { get; set; }
    public Dictionary<string, HashSet<int>>? TrainOnChapters { get; set; }
    public HashSet<string>? InferenceTextIds { get; set; }
    public Dictionary<string, HashSet<int>>? InferenceChapters { get; set; }

    public bool IsFiltered =>
        TrainOnTextIds != null || TrainOnChapters != null || InferenceTextIds != null || InferenceChapters != null;
}
