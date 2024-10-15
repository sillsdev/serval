namespace SIL.ServiceToolkit.Utils;

public interface IParallelCorpusPreprocessingService
{
    void Preprocess(
        IReadOnlyList<ParallelCorpus> corpora,
        Action<Row> train,
        Action<Row, ParallelCorpus> pretranslate,
        bool useKeyTerms = false
    );
}
