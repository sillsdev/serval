namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusPreprocessingService
{
    Task AnalyseCorporaAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<QuoteConventionAnalysis?, QuoteConventionAnalysis?, ParallelCorpus, Task> analyze
    );
    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false
    );
}
