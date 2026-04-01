using SIL.Machine.Corpora;
using SIL.Machine.PunctuationAnalysis;

namespace Serval.Shared.Contracts;

public interface IParallelCorpusService
{
    QuoteConventionAnalysis AnalyzeTargetQuoteConvention(IEnumerable<ParallelCorpusContract> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        IReadOnlyList<UsfmVersificationError> Errors
    )> AnalyzeUsfmVersification(IEnumerable<ParallelCorpusContract> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        MissingParentProjectError
    )> FindMissingParentProjects(IEnumerable<ParallelCorpusContract> parallelCorpora);

    Task PreprocessAsync(
        IEnumerable<ParallelCorpusContract> parallelCorpora,
        Func<ParallelRowContract, TrainingDataType, Task> train,
        Func<ParallelRowContract, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );

    string UpdateSourceUsfm(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<PretranslationContract> pretranslations,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        bool placeParagraphMarkers,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    );

    string UpdateTargetUsfm(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<PretranslationContract> pretranslations,
        UpdateUsfmTextBehavior textBehavior,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    );

    Dictionary<string, List<int>> GetChapters(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string fileLocation,
        string scriptureRange
    );
}
