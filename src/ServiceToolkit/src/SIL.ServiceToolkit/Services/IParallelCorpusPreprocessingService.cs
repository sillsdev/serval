namespace SIL.ServiceToolkit.Utils;

public interface IParallelCorpusPreprocessingService
{
    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, ParallelCorpus, Task> pretranslate,
        bool useKeyTerms = false
    );
}
