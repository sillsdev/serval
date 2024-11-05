namespace Serval.Machine.Shared.Services;

public class SmtTransferPostprocessBuildJob(
    IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<TranslationEngine> buildJobService,
    ILogger<SmtTransferPostprocessBuildJob> logger,
    ISharedFileService sharedFileService,
    IDistributedReaderWriterLockFactory lockFactory,
    IRepository<TrainSegmentPair> trainSegmentPairs,
    ISmtModelFactory smtModelFactory,
    ITruecaserFactory truecaserFactory,
    IOptionsMonitor<BuildJobOptions> buildOptions,
    IOptionsMonitor<SmtTransferEngineOptions> engineOptions
)
    : PostprocessBuildJob(
        platformService,
        engines,
        dataAccessContext,
        buildJobService,
        logger,
        sharedFileService,
        buildOptions
    )
{
    private readonly ISmtModelFactory _smtModelFactory = smtModelFactory;
    private readonly ITruecaserFactory _truecaserFactory = truecaserFactory;
    private readonly IRepository<TrainSegmentPair> _trainSegmentPairs = trainSegmentPairs;
    private readonly IOptionsMonitor<SmtTransferEngineOptions> _engineOptions = engineOptions;
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;

    protected override async Task<int> SaveModelAsync(string engineId, string buildId)
    {
        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId);
        return await @lock.WriterLockAsync(
            async ct =>
            {
                await using (
                    Stream engineStream = await SharedFileService.OpenReadAsync($"builds/{buildId}/model.tar.gz", ct)
                )
                {
                    await _smtModelFactory.UpdateEngineFromAsync(
                        Path.Combine(_engineOptions.CurrentValue.EnginesDir, engineId),
                        engineStream,
                        ct
                    );
                }
                IReadOnlyList<TrainSegmentPair> segmentPairs = await _trainSegmentPairs.GetAllAsync(
                    p => p.TranslationEngineRef == engineId,
                    ct
                );
                TrainOnNewSegmentPairs(engineId, segmentPairs, ct);
                await Engines.UpdateAsync(
                    engineId,
                    u => u.Set(e => e.CollectTrainSegmentPairs, false),
                    cancellationToken: ct
                );
                return segmentPairs.Count;
            },
            _engineOptions.CurrentValue.SaveModelTimeout
        );
    }

    private void TrainOnNewSegmentPairs(
        string engineId,
        IReadOnlyList<TrainSegmentPair> segmentPairs,
        CancellationToken cancellationToken
    )
    {
        string engineDir = Path.Combine(_engineOptions.CurrentValue.EnginesDir, engineId);
        var tokenizer = new LatinWordTokenizer();
        var detokenizer = new LatinWordDetokenizer();
        ITruecaser truecaser = _truecaserFactory.Create(engineDir);
        using IInteractiveTranslationModel smtModel = _smtModelFactory.Create(
            engineDir,
            tokenizer,
            detokenizer,
            truecaser
        );
        foreach (TrainSegmentPair segmentPair in segmentPairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            smtModel.TrainSegment(segmentPair.Source, segmentPair.Target);
        }
        cancellationToken.ThrowIfCancellationRequested();
        smtModel.Save();
    }
}
