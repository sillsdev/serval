using Serval.Shared.Contracts;

namespace SIL.ServiceToolkit.Services;

public interface IParallelCorpusService
{
    QuoteConventionAnalysis AnalyzeTargetQuoteConvention(IEnumerable<FilteredParallelCorpus> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        IReadOnlyList<UsfmVersificationError> Errors
    )> AnalyzeUsfmVersification(IEnumerable<FilteredParallelCorpus> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        MissingParentProjectError
    )> FindMissingParentProjects(IEnumerable<FilteredParallelCorpus> parallelCorpora);

    Task PreprocessAsync(
        IEnumerable<FilteredParallelCorpus> parallelCorpora,
        Func<ParallelRow, TrainingDataType, Task> train,
        Func<ParallelRow, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );

    string UpdateSourceUsfm(
        IReadOnlyList<FilteredParallelCorpus> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<PretranslationData> pretranslations,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        bool placeParagraphMarkers,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    );

    string UpdateTargetUsfm(
        IReadOnlyList<FilteredParallelCorpus> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<PretranslationData> pretranslations,
        UpdateUsfmTextBehavior textBehavior,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    );

    Dictionary<string, List<int>> GetChapters(
        IReadOnlyList<FilteredParallelCorpus> parallelCorpora,
        string fileLocation,
        string scriptureRange
    );
}
