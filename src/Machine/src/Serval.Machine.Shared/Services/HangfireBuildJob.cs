namespace Serval.Machine.Shared.Services;

public abstract class HangfireBuildJob<TEngine>(
    IPlatformService platformService,
    IRepository<TEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<TEngine> buildJobService,
    ILogger<HangfireBuildJob<TEngine>> logger
) : HangfireBuildJob<TEngine, object?>(platformService, engines, dataAccessContext, buildJobService, logger)
    where TEngine : ITrainingEngine
{
    public virtual Task RunAsync(
        string engineId,
        string buildId,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        return RunAsync(engineId, buildId, null, buildOptions, cancellationToken);
    }
}

public abstract class HangfireBuildJob<TEngine, TData>(
    IPlatformService platformService,
    IRepository<TEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<TEngine> buildJobService,
    ILogger<HangfireBuildJob<TEngine, TData>> logger
)
    where TEngine : ITrainingEngine
{
    protected IPlatformService PlatformService { get; } = platformService;
    protected IRepository<TEngine> Engines { get; } = engines;
    protected IDataAccessContext DataAccessContext { get; } = dataAccessContext;
    protected IBuildJobService<TEngine> BuildJobService { get; } = buildJobService;
    protected ILogger<HangfireBuildJob<TEngine, TData>> Logger { get; } = logger;

    public virtual async Task RunAsync(
        string engineId,
        string buildId,
        TData data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        JobCompletionStatus completionStatus = JobCompletionStatus.Completed;
        try
        {
            await InitializeAsync(engineId, buildId, data, cancellationToken);
            if (!await BuildJobService.BuildJobStartedAsync(engineId, buildId, cancellationToken))
            {
                completionStatus = JobCompletionStatus.Canceled;
                return;
            }

            await DoWorkAsync(engineId, buildId, data, buildOptions, cancellationToken);
        }
        catch (OperationCanceledException e)
        {
            // Log the full exception for debugging purposes
            Logger.LogInformation(e, "Build Hangfire job canceled ({0})", buildId);

            // Check if the cancellation was initiated by an API call or a shutdown.
            TEngine? engine = await Engines.GetAsync(
                e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.BuildId == buildId,
                CancellationToken.None
            );
            if (engine?.CurrentBuild?.JobState is BuildJobState.Canceling)
            {
                completionStatus = JobCompletionStatus.Canceled;
                await DataAccessContext.WithTransactionAsync(
                    async (ct) =>
                    {
                        await PlatformService.BuildCanceledAsync(buildId, CancellationToken.None);
                        await BuildJobService.BuildJobFinishedAsync(
                            engineId,
                            buildId,
                            buildComplete: false,
                            CancellationToken.None
                        );
                    },
                    cancellationToken: CancellationToken.None
                );
                Logger.LogInformation("Build canceled ({0})", buildId);
            }
            else if (engine is not null)
            {
                // the build was canceled, because of a server shutdown
                // switch state back to pending
                completionStatus = JobCompletionStatus.Restarting;
                await DataAccessContext.WithTransactionAsync(
                    async (ct) =>
                    {
                        await PlatformService.BuildRestartingAsync(buildId, CancellationToken.None);
                        await BuildJobService.BuildJobRestartingAsync(engineId, buildId, CancellationToken.None);
                    },
                    cancellationToken: CancellationToken.None
                );
                Logger.LogInformation("Build restarting ({0})", buildId);
                throw;
            }
            else
            {
                Logger.LogInformation("Build engine not found ({0})", buildId);
                completionStatus = JobCompletionStatus.Canceled;
            }
        }
        catch (Exception e)
        {
            completionStatus = JobCompletionStatus.Faulted;
            await DataAccessContext.WithTransactionAsync(
                async (ct) =>
                {
                    await PlatformService.BuildFaultedAsync(buildId, e.Message, CancellationToken.None);
                    await BuildJobService.BuildJobFinishedAsync(
                        engineId,
                        buildId,
                        buildComplete: false,
                        CancellationToken.None
                    );
                },
                cancellationToken: CancellationToken.None
            );
            Logger.LogError(0, e, "Build faulted ({0})", buildId);
            throw;
        }
        finally
        {
            await CleanupAsync(engineId, buildId, data, completionStatus);
        }
    }

    protected virtual Task InitializeAsync(
        string engineId,
        string buildId,
        TData data,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }

    protected abstract Task DoWorkAsync(
        string engineId,
        string buildId,
        TData data,
        string? buildOptions,
        CancellationToken cancellationToken
    );

    protected virtual Task CleanupAsync(
        string engineId,
        string buildId,
        TData data,
        JobCompletionStatus completionStatus
    )
    {
        return Task.CompletedTask;
    }

    protected enum JobCompletionStatus
    {
        Completed,
        Faulted,
        Canceled,
        Restarting,
    }
}
