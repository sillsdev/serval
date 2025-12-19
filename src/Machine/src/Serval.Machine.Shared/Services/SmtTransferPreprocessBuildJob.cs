namespace Serval.Machine.Shared.Services;

public class SmtTransferPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<SmtTransferPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    IDistributedReaderWriterLockFactory lockFactory,
    IRepository<TrainSegmentPair> trainSegmentPairs,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService,
    IOptionsMonitor<BuildJobOptions> options
)
    : TranslationPreprocessBuildJob(
        platformService,
        engines,
        dataAccessContext,
        logger,
        buildJobService,
        sharedFileService,
        parallelCorpusPreprocessingService,
        options
    )
{
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;
    private readonly IRepository<TrainSegmentPair> _trainSegmentPairs = trainSegmentPairs;

    protected override async Task InitializeAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> data,
        CancellationToken cancellationToken
    )
    {
        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        await @lock.WriterLockAsync(
            async ct =>
            {
                await _trainSegmentPairs.DeleteAllAsync(p => p.TranslationEngineRef == engineId, ct);
                await Engines.UpdateAsync(
                    engineId,
                    u => u.Set(e => e.CollectTrainSegmentPairs, true),
                    cancellationToken: ct
                );
            },
            cancellationToken: cancellationToken
        );
    }
}
