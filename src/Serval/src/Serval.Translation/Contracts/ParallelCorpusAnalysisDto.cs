namespace Serval.Translation.Contracts;

public record ParallelCorpusAnalysisDto
{
    public required string ParallelCorpusRef { get; init; }

    [Obsolete]
    public required string SourceQuoteConvention { get; init; }
    public required string TargetQuoteConvention { get; init; }
}
