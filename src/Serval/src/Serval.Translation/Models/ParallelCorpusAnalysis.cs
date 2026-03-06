namespace Serval.Translation.Models;

public record ParallelCorpusAnalysis
{
    public required string ParallelCorpusRef { get; init; }
    public required string TargetQuoteConvention { get; init; }
}
