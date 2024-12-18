namespace Serval.Machine.Shared.Services;

public class StatisticalPostprocessBuildJob(
    IEnumerable<IPlatformService> platformServices,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ILogger<StatisticalPostprocessBuildJob> logger,
    ISharedFileService sharedFileService,
    IDistributedReaderWriterLockFactory lockFactory,
    ISmtModelFactory smtModelFactory,
    IOptionsMonitor<BuildJobOptions> buildOptions,
    IOptionsMonitor<WordAlignmentEngineOptions> engineOptions
)
    : PostprocessBuildJob<WordAlignmentEngine>(
        platformServices.First(ps => ps.EngineGroup == EngineGroup.WordAlignment),
        engines,
        dataAccessContext,
        buildJobService,
        logger,
        sharedFileService,
        buildOptions
    )
{
    private readonly ISmtModelFactory _smtModelFactory = smtModelFactory;
    private readonly IOptionsMonitor<WordAlignmentEngineOptions> _engineOptions = engineOptions;
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
                $"builds/{buildId}/word_alignment_outputs.json",
                cancellationToken
            )
        )
        {
            await PlatformService.InsertInferencesAsync(engineId, wordAlignmentStream, cancellationToken);
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
                return 0;
            },
            _engineOptions.CurrentValue.SaveModelTimeout
        );
    }
}
