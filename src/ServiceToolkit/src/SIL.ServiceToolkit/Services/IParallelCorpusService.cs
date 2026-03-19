namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusService
{
    QuoteConventionAnalysis AnalyzeTargetQuoteConvention(IEnumerable<ParallelCorpus> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        IReadOnlyList<UsfmVersificationError> Errors
    )> AnalyzeUsfmVersification(IEnumerable<ParallelCorpus> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        MissingParentProjectError
    )> FindMissingParentProjects(IEnumerable<ParallelCorpus> parallelCorpora);

    Task PreprocessAsync(
        IEnumerable<ParallelCorpus> parallelCorpora,
        Func<Row, TrainingDataType, Task> train,
        Func<Row, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );

    string UpdateSourceUsfm(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<ParallelRow> rows,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        bool placeParagraphMarkers,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    );

    string UpdateTargetUsfm(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<ParallelRow> rows,
        UpdateUsfmTextBehavior textBehavior,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    );

    Dictionary<string, List<int>> GetChapters(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        string fileLocation,
        string scriptureRange
    );
}
