namespace Serval.Machine.Shared.Services;

public class EchoTrainingBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<EchoTrainingBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService
) : BuildJob<TranslationEngine, object?>(platformService, engines, dataAccessContext, buildJobService, logger)
{
    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        object? data,
        string? buildOptions,
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
            data: (0, 0.0),
            buildOptions: buildOptions,
            cancellationToken: cancellationToken
        );
        if (canceling)
            throw new OperationCanceledException();
    }
}
