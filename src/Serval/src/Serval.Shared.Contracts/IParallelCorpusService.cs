namespace Serval.Shared.Contracts;

public interface IParallelCorpusService
{
    string AnalyzeTargetQuoteConvention(IEnumerable<ParallelCorpusContract> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        IReadOnlyList<UsfmVersificationErrorContract> Errors
    )> AnalyzeUsfmVersification(IEnumerable<ParallelCorpusContract> parallelCorpora);

    IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        MissingParentProjectErrorContract Error
    )> FindMissingParentProjects(IEnumerable<ParallelCorpusContract> parallelCorpora);

    Task PreprocessAsync(
        IEnumerable<ParallelCorpusContract> parallelCorpora,
        Func<ParallelRowContract, TrainingDataType, Task> train,
        Func<ParallelRowContract, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    );
}
