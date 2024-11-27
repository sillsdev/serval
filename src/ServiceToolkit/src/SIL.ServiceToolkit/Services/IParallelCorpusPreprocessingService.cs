using Nito.AsyncEx;

namespace SIL.ServiceToolkit.Utils;

public interface IParallelCorpusPreprocessingService
{
    Task Preprocess(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, ParallelCorpus, Task> pretranslate,
        bool useKeyTerms = false
    );
}
