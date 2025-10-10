namespace Serval.Shared.Models;

public record ParallelCorpusAnalysis
{
    public required string ParallelCorpusRef { get; init; }
    public required string TargetQuoteConvention { get; init; }
    public bool CanDenormalizeQuotes { get; init; }
}
