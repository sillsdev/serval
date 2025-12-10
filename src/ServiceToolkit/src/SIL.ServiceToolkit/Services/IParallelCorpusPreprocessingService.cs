namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusPreprocessingService
{
    QuoteConventionAnalysis? AnalyzeTargetCorpusQuoteConvention(ParallelCorpus corpus);
    IReadOnlyList<(string CorpusId, IReadOnlyList<UsfmVersificationError> Errors)> AnalyzeUsfmVersification(
        ParallelCorpus corpus
    );

    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, bool, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );
}
