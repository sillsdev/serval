namespace Serval.Translation.Contracts;

public record ParallelCorpusAnalysisDto
{
    public required string ParallelCorpusRef { get; init; }
    public required string SourceQuoteConvention { get; init; }
    public required string TargetQuoteConvention { get; init; }
}
