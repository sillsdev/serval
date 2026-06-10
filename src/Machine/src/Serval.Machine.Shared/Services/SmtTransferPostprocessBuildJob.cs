namespace Serval.Machine.Shared.Services;

public class SmtTransferPostprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<TranslationEngine> buildJobService,
    ILogger<SmtTransferPostprocessBuildJob> logger,
    ISharedFileService sharedFileService,
    SmtTransferEngineStateService stateService,
    IRepository<TrainSegmentPair> trainSegmentPairs,
    ISmtModelFactory smtModelFactory,
    ITruecaserFactory truecaserFactory,
    IOptionsMonitor<BuildJobOptions> buildOptions,
    IOptionsMonitor<SmtTransferEngineOptions> engineOptions
)
    : TranslationPostprocessBuildJob(
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
    private readonly SmtTransferEngineStateService _stateService = stateService;

    protected override async Task<int> SaveModelAsync(
        string engineId,
        string buildId,
        CancellationToken cancellationToken
    )
    {
        SmtTransferEngineState state = _stateService.Get(engineId);
        using (await state.Lock.WriterLockAsync(cancellationToken))
        {
            // Save the model to a temporary directory on Windows to avoid file locking issues. The directory will
            // be moved the next time the engine is used.
            string engineDir = Path.Combine(
                _engineOptions.CurrentValue.EnginesDir,
                OperatingSystem.IsWindows() ? engineId + "-new" : engineId
            );
            await using (
                Stream engineStream = await SharedFileService.OpenReadAsync(
                    $"builds/{buildId}/model.tar.gz",
                    cancellationToken
                )
            )
            {
                await _smtModelFactory.UpdateEngineFromAsync(engineDir, engineStream, cancellationToken);
            }
            IReadOnlyList<TrainSegmentPair> segmentPairs = await _trainSegmentPairs.GetAllAsync(
                p => p.TranslationEngineRef == engineId,
                cancellationToken
            );
            TrainOnNewSegmentPairs(engineDir, segmentPairs, cancellationToken);
            await Engines.UpdateAsync(
                engineId,
                u => u.Set(e => e.CollectTrainSegmentPairs, false),
                cancellationToken: cancellationToken
            );
            return segmentPairs.Count;
        }
    }

    private void TrainOnNewSegmentPairs(
        string engineDir,
        IReadOnlyList<TrainSegmentPair> segmentPairs,
        CancellationToken cancellationToken
    )
    {
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
