namespace Serval.Machine.Translation.Services;

public class EchoPostprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<TranslationEngine> buildJobService,
    ILogger<EchoPostprocessBuildJob> logger,
    ISharedFileService sharedFileService,
    IOptionsMonitor<BuildJobOptions> options
)
    : PostprocessBuildJob<TranslationEngine>(
        platformService,
        engines,
        dataAccessContext,
        buildJobService,
        logger,
        sharedFileService,
        options
    )
{
    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        (int, double) data,
        string? buildOptions,
        string? model,
        CancellationToken cancellationToken
    )
    {
        (int corpusSize, double confidence) = data;

        await DataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await PlatformService.BuildCompletedAsync(
                    buildId,
                    corpusSize,
                    Math.Round(confidence, 2, MidpointRounding.AwayFromZero),
                    ct
                );
                await BuildJobService.BuildJobFinishedAsync(engineId, buildId, buildComplete: true, ct);
            },
            cancellationToken: CancellationToken.None
        );

        Logger.LogInformation("Build completed ({0}).", buildId);
    }

    protected override Task CleanupAsync(string engineId, string buildId, JobCompletionStatus completionStatus) =>
        Task.CompletedTask;
}
