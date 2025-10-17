namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusPreprocessingService
{
    QuoteConventionAnalysis? AnalyzeTargetCorpusQuoteConvention(ParallelCorpus corpus);
    IReadOnlyList<(string CorpusId, IReadOnlyList<UsfmVersificationMismatch> Mismatches)> AnalyzeUsfmVersification(
        ParallelCorpus corpus
    );

    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false
    );
}
