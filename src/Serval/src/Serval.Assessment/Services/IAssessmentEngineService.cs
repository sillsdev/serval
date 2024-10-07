namespace Serval.Assessment.Services;

public interface IAssessmentEngineService : IEngineServiceBase
{
    Task<IEnumerable<AssessmentEngine>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<AssessmentEngine> GetAsync(string engineId, CancellationToken cancellationToken = default);

    Task<AssessmentEngine> CreateAsync(AssessmentEngine engine, CancellationToken cancellationToken = default);
    Task DeleteAsync(string engineId, CancellationToken cancellationToken = default);
    Task StartJobAsync(AssessmentBuild build, CancellationToken cancellationToken = default);
    Task<bool> CancelJobAsync(string engineId, string jobId, CancellationToken cancellationToken = default);
    Task<Corpus> ReplaceCorpusAsync(string id, Corpus corpus, CancellationToken cancellationToken = default);
    Task<Corpus> ReplaceReferenceCorpusAsync(
        string id,
        Corpus referenceCorpus,
        CancellationToken cancellationToken = default
    );
}
