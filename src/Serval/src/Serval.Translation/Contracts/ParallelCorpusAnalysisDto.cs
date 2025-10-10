namespace Serval.Translation.Contracts;

public record ParallelCorpusAnalysisDto
{
    public required string ParallelCorpusRef { get; init; }
#pragma warning disable CS9042
    [Obsolete]
    public required string SourceQuoteConvention { get; init; }
#pragma warning restore CS9042
    public required string TargetQuoteConvention { get; init; }
    public bool CanDenormalizeQuotes { get; init; }
}
