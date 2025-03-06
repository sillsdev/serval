namespace Serval.Machine.Shared.Services;

public class TranslationPostprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<TranslationEngine> buildJobService,
    ILogger<TranslationPostprocessBuildJob> logger,
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
        CancellationToken cancellationToken
    )
    {
        (int corpusSize, double confidence) = data;

        await using (
            Stream pretranslationsStream = await SharedFileService.OpenReadAsync(
                $"builds/{buildId}/pretranslate.trg.json",
                cancellationToken
            )
        )
        {
            await PlatformService.InsertInferenceResultsAsync(engineId, pretranslationsStream, cancellationToken);
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
}
