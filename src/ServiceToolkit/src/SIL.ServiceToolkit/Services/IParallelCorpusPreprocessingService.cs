namespace SIL.ServiceToolkit.Utils;

public interface IParallelCorpusPreprocessingService
{
    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false
    );
}
