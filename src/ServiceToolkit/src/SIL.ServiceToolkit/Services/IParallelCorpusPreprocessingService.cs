namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusPreprocessingService
{
    QuoteConventionAnalysis? AnalyzeTargetCorpusQuoteConvention(ParallelCorpus corpus);
    IReadOnlyList<(string CorpusId, IReadOnlyList<UsfmVersificationError> Errors)> AnalyzeUsfmVersification(
        ParallelCorpus parallelCorpus
    );

    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, TrainingDataType, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );
}
