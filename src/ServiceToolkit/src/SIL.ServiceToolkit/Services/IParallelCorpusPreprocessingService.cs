namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusPreprocessingService
{
    QuoteConventionAnalysis? AnalyzeTargetCorpusQuoteConvention(
        ParallelCorpus parallelCorpus,
        IReadOnlyList<CorpusFile>? referenceFiles = null
    );
    IReadOnlyList<(string CorpusId, IReadOnlyList<UsfmVersificationError> Errors)> AnalyzeUsfmVersification(
        ParallelCorpus parallelCorpus,
        IReadOnlyList<CorpusFile>? referenceFiles = null
    );

    IReadOnlyList<MissingParentProjectError> FindMissingParentProjects(IReadOnlyList<ParallelCorpus> corpora);

    Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, TrainingDataType, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );
}
