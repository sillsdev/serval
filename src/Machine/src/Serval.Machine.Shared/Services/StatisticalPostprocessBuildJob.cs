namespace Serval.Machine.Shared.Services;

public class StatisticalPostprocessBuildJob(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ILogger<StatisticalPostprocessBuildJob> logger,
    ISharedFileService sharedFileService,
    IDistributedReaderWriterLockFactory lockFactory,
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
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;

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
            await PlatformService.InsertInferenceResultsAsync(engineId, wordAlignmentStream, cancellationToken);
        }

        int additionalCorpusSize = await SaveModelAsync(engineId, buildId);
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

    protected override async Task<int> SaveModelAsync(string engineId, string buildId)
    {
        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId);
        return await @lock.WriterLockAsync(
            async ct =>
            {
                // Save the model to a temporary directory on Windows to avoid file locking issues. The directory will
                // be moved the next time the engine is used.
                string engineDir = Path.Combine(
                    _engineOptions.CurrentValue.EnginesDir,
                    OperatingSystem.IsWindows() ? engineId + "-new" : engineId
                );
                await using Stream engineStream = await SharedFileService.OpenReadAsync(
                    $"builds/{buildId}/model.tar.gz",
                    ct
                );

                await _wordAlignmentModelFactory.UpdateEngineFromAsync(engineDir, engineStream, ct);
                return 0;
            },
            _engineOptions.CurrentValue.SaveModelTimeout
        );
    }
}
