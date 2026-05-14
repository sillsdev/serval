namespace Serval.Machine.Shared.Services;

public class BuildJobRunnerManager<TEngine>(IServiceProvider services, ILogger<RecurrentTask> logger)
    : RecurrentTask("Build job runner manager", services, RefreshPeriod, logger)
    where TEngine : ITrainingEngine
{
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromSeconds(5);

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        EngineGroup engineGroup = GetEngineGroup<TEngine>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BuildJobRunnerManager<TEngine>>>();
        var dataAccessContext = scope.ServiceProvider.GetRequiredService<IDataAccessContext>();
        var platformService = scope.ServiceProvider.GetRequiredKeyedService<IPlatformService>(engineGroup);
        var runners = scope
            .ServiceProvider.GetRequiredService<IEnumerable<IBuildJobRunner>>()
            .ToDictionary(r => r.Type);
        var engines = scope.ServiceProvider.GetRequiredService<IRepository<TEngine>>();

        await DispatchQueuedBuildJobsAsync(
            engines,
            runners,
            logger,
            dataAccessContext,
            platformService,
            cancellationToken
        );
        await StopCancelingBuildJobsAsync(engines, runners, logger, cancellationToken);
        await DeleteDeletingEngines(engines, runners, logger, cancellationToken);
    }

    private static async Task DispatchQueuedBuildJobsAsync(
        IRepository<TEngine> engines,
        IReadOnlyDictionary<BuildJobRunnerType, IBuildJobRunner> runners,
        ILogger<BuildJobRunnerManager<TEngine>> logger,
        IDataAccessContext dataAccessContext,
        IPlatformService platformService,
        CancellationToken cancellationToken
    )
    {
        foreach (
            TEngine engine in await engines.GetAllAsync(
                e => e.CurrentBuild != null && e.CurrentBuild.JobState == BuildJobState.Queued,
                cancellationToken
            )
        )
        {
            Build build = engine.CurrentBuild!;
            if (!string.IsNullOrEmpty(build.JobId))
                //TODO - should these be cleaned up?
                continue;

            string? jobId = null;
            try
            {
                await dataAccessContext.WithTransactionAsync(
                    async (ct) =>
                    {
                        await engines.UpdateAsync(
                            e => e.EngineId == engine.Id,
                            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Pending),
                            cancellationToken: ct
                        );
                        jobId = await runners[build.BuildJobRunner]
                            .CreateJobAsync(
                                engine.Type,
                                engine.EngineId,
                                build.BuildId,
                                build.Stage,
                                build.Options,
                                ct
                            );
                        await runners[build.BuildJobRunner].EnqueueJobAsync(jobId, engine.Type, cancellationToken);
                    },
                    cancellationToken: CancellationToken.None
                );
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to dispatch build job for build {BuildId}.", build.BuildId);
                await dataAccessContext.WithTransactionAsync(
                    async (ct) =>
                    {
                        await platformService.BuildFaultedAsync(build.BuildId, e.Message, CancellationToken.None);
                        await engines.UpdateAsync(
                            e =>
                                e.EngineId == engine.Id
                                && e.CurrentBuild != null
                                && e.CurrentBuild.BuildId == build.BuildId,
                            u =>
                            {
                                u.Unset(e => e.CurrentBuild);
                            },
                            cancellationToken: cancellationToken
                        );
                        if (jobId != null)
                            await runners[build.BuildJobRunner].DeleteJobAsync(jobId, CancellationToken.None);
                    },
                    cancellationToken: CancellationToken.None
                );
            }
        }
    }

    private static async Task StopCancelingBuildJobsAsync(
        IRepository<TEngine> engines,
        IReadOnlyDictionary<BuildJobRunnerType, IBuildJobRunner> runners,
        ILogger<BuildJobRunnerManager<TEngine>> logger,
        CancellationToken cancellationToken
    )
    {
        foreach (
            TEngine engine in await engines.GetAllAsync(
                e => e.CurrentBuild != null && e.CurrentBuild.JobState == BuildJobState.Canceling,
                cancellationToken
            )
        )
        {
            Build build = engine.CurrentBuild!;
            if (string.IsNullOrEmpty(build.JobId))
                //TODO - should these be cleaned up?
                continue;

            try
            {
                await runners[build.BuildJobRunner].StopJobAsync(build.JobId, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(
                    e,
                    "Failed to stop job {JobId} for canceling build {BuildId}.",
                    build.JobId,
                    build.BuildId
                );
            }
        }
    }

    private static async Task DeleteDeletingEngines(
        IRepository<TEngine> engines,
        IReadOnlyDictionary<BuildJobRunnerType, IBuildJobRunner> runners,
        ILogger<BuildJobRunnerManager<TEngine>> logger,
        CancellationToken cancellationToken
    )
    {
        //TODO what about non-building engines? For ClearML this would still be needed
        foreach (
            TEngine engine in await engines.GetAllAsync(
                e => e.CurrentBuild != null && e.CurrentBuild.JobState == BuildJobState.Deleting,
                cancellationToken
            )
        )
        {
            foreach (BuildJobRunnerType runnerType in runners.Keys)
            {
                IBuildJobRunner runner = runners[runnerType];
                try
                {
                    await runner.DeleteEngineAsync(engine.Id, cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to delete engine {EngineId}.", engine.EngineId);
                }
            }
        }
    }

    private static EngineGroup GetEngineGroup<T>()
        where T : ITrainingEngine
    {
        //TODO is there a better way? Could just explicitly create translation and alignment managers?
        return typeof(T).Name switch
        {
            nameof(TranslationEngine) => EngineGroup.Translation,
            nameof(WordAlignmentEngine) => EngineGroup.WordAlignment,
            _ => throw new InvalidOperationException($"Unknown engine type: {typeof(T).Name}"),
        };
    }
}
