namespace Serval.Translation.Contracts;

public record CorpusAnalysisDto
{
    public required string CorpusRef { get; init; }
    public required string SourceQuoteConvention { get; init; }
    public required string TargetQuoteConvention { get; init; }
}
