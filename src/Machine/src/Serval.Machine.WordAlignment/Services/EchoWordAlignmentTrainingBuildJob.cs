namespace Serval.Machine.WordAlignment.Services;

public class EchoWordAlignmentTrainingBuildJob(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<EchoWordAlignmentTrainingBuildJob> logger,
    IBuildJobService<WordAlignmentEngine> buildJobService
) : BuildJob<WordAlignmentEngine, object?>(platformService, engines, dataAccessContext, buildJobService, logger)
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
        WordAlignmentEngine? engine = await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
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
