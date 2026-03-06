namespace Serval.Translation.Dtos;

public record ParallelCorpusAnalysisDto
{
    public required string ParallelCorpusRef { get; init; }

    [Obsolete]
    public string? SourceQuoteConvention { get; init; }
    public required string TargetQuoteConvention { get; init; }
    public bool CanDenormalizeQuotes { get; init; }
}
