namespace Serval.Machine.Shared.Models;

public record BuildData
{
    public IReadOnlyList<ParallelCorpusContract>? ParallelCorpora { get; init; }
    public double? Confidence { get; init; }
    public int? CorpusSize { get; init; }
}
