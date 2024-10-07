namespace Serval.Shared.Services;

public interface ITrainingEngineService<TBuild, TEngine> : IEngineServiceBase
{
    Task<IEnumerable<TEngine>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<TEngine> GetAsync(string engineId, CancellationToken cancellationToken = default);

    Task<TEngine> CreateAsync(TEngine engine, CancellationToken cancellationToken = default);
    Task DeleteAsync(string engineId, CancellationToken cancellationToken = default);
    Task StartJobAsync(TBuild build, CancellationToken cancellationToken = default);

    Task<bool> CancelJobAsync(string engineId, CancellationToken cancellationToken = default);

    Task<Queue> GetQueueAsync(string engineType, CancellationToken cancellationToken = default);
    Task AddCorpusAsync(string engineId, TrainingCorpus corpus, CancellationToken cancellationToken = default);
    Task<TrainingCorpus> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IReadOnlyList<CorpusFile>? sourceFiles,
        IReadOnlyList<CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    );
    Task DeleteCorpusAsync(
        string engineId,
        string corpusId,
        bool deleteFiles,
        CancellationToken cancellationToken = default
    );
}
