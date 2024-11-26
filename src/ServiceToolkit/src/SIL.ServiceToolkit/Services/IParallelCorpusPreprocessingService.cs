using Nito.AsyncEx;

namespace SIL.ServiceToolkit.Utils;

public interface IParallelCorpusPreprocessingService
{
    Task Preprocess(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Action<Row, ParallelCorpus> pretranslate,
        bool useKeyTerms = false
    );
}
