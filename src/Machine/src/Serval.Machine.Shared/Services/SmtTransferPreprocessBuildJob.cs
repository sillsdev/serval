namespace Serval.Machine.Shared.Services;

public class SmtTransferPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<SmtTransferPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    SmtTransferEngineStateService stateService,
    IRepository<TrainSegmentPair> trainSegmentPairs,
    IParallelCorpusService parallelCorpusService,
    IOptionsMonitor<BuildJobOptions> options
)
    : TranslationPreprocessBuildJob(
        platformService,
        engines,
        dataAccessContext,
        logger,
        buildJobService,
        sharedFileService,
        parallelCorpusService,
        options
    )
{
    private readonly SmtTransferEngineStateService _stateService = stateService;
    private readonly IRepository<TrainSegmentPair> _trainSegmentPairs = trainSegmentPairs;

    protected override async Task InitializeAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> data,
        CancellationToken cancellationToken
    )
    {
        SmtTransferEngineState state = _stateService.Get(engineId);
        using (await state.Lock.WriterLockAsync(cancellationToken))
        {
            await _trainSegmentPairs.DeleteAllAsync(p => p.TranslationEngineRef == engineId, cancellationToken);
            await Engines.UpdateAsync(
                engineId,
                u => u.Set(e => e.CollectTrainSegmentPairs, true),
                cancellationToken: cancellationToken
            );
        }
    }
}
