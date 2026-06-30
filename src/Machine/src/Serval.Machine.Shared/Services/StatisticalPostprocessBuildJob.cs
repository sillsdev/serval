namespace Serval.Machine.Shared.Services;

public class StatisticalPostprocessBuildJob(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ILogger<StatisticalPostprocessBuildJob> logger,
    ISharedFileService sharedFileService,
    StatisticalEngineStateService stateService,
    IWordAlignmentModelFactory wordAlignmentModelFactory,
    IOptionsMonitor<BuildJobOptions> buildOptions,
    IOptionsMonitor<StatisticalEngineOptions> engineOptions
)
    : PostprocessBuildJob<WordAlignmentEngine>(
        platformService,
        engines,
        dataAccessContext,
        buildJobService,
        logger,
        sharedFileService,
        buildOptions
    )
{
    private readonly IWordAlignmentModelFactory _wordAlignmentModelFactory = wordAlignmentModelFactory;
    private readonly IOptionsMonitor<StatisticalEngineOptions> _engineOptions = engineOptions;
    private readonly StatisticalEngineStateService _stateService = stateService;

    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        (int, double) data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        (int corpusSize, double confidence) = data;

        await using (
            Stream wordAlignmentStream = await SharedFileService.OpenReadAsync(
                $"builds/{buildId}/word_alignments.outputs.json",
                cancellationToken
            )
        )
        {
            await PlatformService.InsertInferenceResultsAsync(
                engineId,
                buildId,
                wordAlignmentStream,
                cancellationToken
            );
        }

        int additionalCorpusSize = await SaveModelAsync(engineId, buildId, cancellationToken);
        await DataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await PlatformService.BuildCompletedAsync(
                    buildId,
                    corpusSize + additionalCorpusSize,
                    Math.Round(confidence, 2, MidpointRounding.AwayFromZero),
                    ct
                );
                await BuildJobService.BuildJobFinishedAsync(engineId, buildId, buildComplete: true, ct);
            },
            cancellationToken: CancellationToken.None
        );

        Logger.LogInformation("Build completed ({0}).", buildId);
    }

    protected override async Task<int> SaveModelAsync(
        string engineId,
        string buildId,
        CancellationToken cancellationToken
    )
    {
        StatisticalEngineState state = _stateService.Get(engineId);
        using (await AcquireWriteLockAsync(state.Lock, cancellationToken))
        {
            // Save the model to a temporary directory on Windows to avoid file locking issues. The directory will
            // be moved the next time the engine is used.
            string engineDir = Path.Combine(
                _engineOptions.CurrentValue.EnginesDir,
                OperatingSystem.IsWindows() ? engineId + "-new" : engineId
            );
            await using Stream engineStream = await SharedFileService.OpenReadAsync(
                $"builds/{buildId}/model.tar.gz",
                cancellationToken
            );

            await _wordAlignmentModelFactory.UpdateEngineFromAsync(engineDir, engineStream, cancellationToken);
            return 0;
        }
    }
}
