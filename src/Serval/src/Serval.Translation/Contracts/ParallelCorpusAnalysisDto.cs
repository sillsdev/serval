namespace Serval.Translation.Contracts;

public record ParallelCorpusAnalysisDto
{
    public required string ParallelCorpusRef { get; init; }
    public required string SourceQuoteConvention
    {
        [Obsolete]
        get;
        init;
    }
    public required string TargetQuoteConvention { get; init; }
    public bool CanDenormalizeQuotes { get; init; }
}
