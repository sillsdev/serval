namespace Serval.Machine.Translation.Services;

public class LlmRemoteTrainingBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<LlmRemoteTrainingBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService
) : BuildJob<TranslationEngine, object?>(platformService, engines, dataAccessContext, buildJobService, logger)
{
    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        object? data,
        string? buildOptions,
        string? model,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new OperationCanceledException($"Engine {engineId} does not exist.  Build canceled.");

        bool canceling = !await BuildJobService.StartBuildJobAsync(
            BuildJobRunnerType.Local,
            engine.Type,
            engineId,
            buildId,
            BuildStage.Postprocess,
            data: (0, 1.0),
            buildOptions: buildOptions,
            model: model,
            cancellationToken: cancellationToken
        );
        if (canceling)
            throw new OperationCanceledException();
    }
}
