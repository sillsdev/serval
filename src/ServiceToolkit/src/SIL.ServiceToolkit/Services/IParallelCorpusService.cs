namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusService
{
    QuoteConventionAnalysis AnalyzeTargetQuoteConvention(CorpusBundle corpusBundle);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        IReadOnlyList<UsfmVersificationError> Errors
    )> AnalyzeUsfmVersification(CorpusBundle corpusBundle);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        MissingParentProjectError
    )> FindMissingParentProjects(CorpusBundle corpusBundle);

    Task PreprocessAsync(
        CorpusBundle corpusBundle,
        Func<Row, TrainingDataType, Task> train,
        Func<Row, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );
}
