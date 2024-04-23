namespace Serval.Aqua.Shared.Models;

public record CorpusFilter
{
    public required bool IncludeAll { get; init; }
    public IReadOnlyDictionary<string, HashSet<int>>? IncludeChapters { get; init; }
    public required HashSet<string> IncludeTextIds { get; init; }
}
