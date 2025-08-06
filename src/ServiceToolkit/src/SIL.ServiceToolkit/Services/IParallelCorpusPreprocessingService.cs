namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusPreprocessingService
{
    (QuoteConventionAnalysis?, QuoteConventionAnalysis?) AnalyzeParallelCorpus(ParallelCorpus corpus);
    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false
    );
}
