﻿namespace Serval.Machine.Shared.Services;

public abstract class HangfireBuildJob(
    IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDistributedReaderWriterLockFactory lockFactory,
    IDataAccessContext dataAccessContext,
    IBuildJobService buildJobService,
    ILogger<HangfireBuildJob> logger
) : HangfireBuildJob<object?>(platformService, engines, lockFactory, dataAccessContext, buildJobService, logger)
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

public abstract class HangfireBuildJob<T>(
    IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDistributedReaderWriterLockFactory lockFactory,
    IDataAccessContext dataAccessContext,
    IBuildJobService buildJobService,
    ILogger<HangfireBuildJob<T>> logger
)
{
    protected IPlatformService PlatformService { get; } = platformService;
    protected IRepository<TranslationEngine> Engines { get; } = engines;
    protected IDistributedReaderWriterLockFactory LockFactory { get; } = lockFactory;
    protected IDataAccessContext DataAccessContext { get; } = dataAccessContext;
    protected IBuildJobService BuildJobService { get; } = buildJobService;
    protected ILogger<HangfireBuildJob<T>> Logger { get; } = logger;

    public virtual async Task RunAsync(
        string engineId,
        string buildId,
        T data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        IDistributedReaderWriterLock @lock = await LockFactory.CreateAsync(engineId, cancellationToken);
        JobCompletionStatus completionStatus = JobCompletionStatus.Completed;
        try
        {
            await InitializeAsync(engineId, buildId, data, @lock, cancellationToken);
            await using (await @lock.WriterLockAsync(cancellationToken: cancellationToken))
            {
                if (!await BuildJobService.BuildJobStartedAsync(engineId, buildId, cancellationToken))
                {
                    completionStatus = JobCompletionStatus.Canceled;
                    return;
                }
            }

            await DoWorkAsync(engineId, buildId, data, buildOptions, @lock, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Check if the cancellation was initiated by an API call or a shutdown.
            TranslationEngine? engine = await Engines.GetAsync(
                e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.BuildId == buildId,
                CancellationToken.None
            );
            if (engine?.CurrentBuild?.JobState is BuildJobState.Canceling)
            {
                completionStatus = JobCompletionStatus.Canceled;
                await using (await @lock.WriterLockAsync(cancellationToken: CancellationToken.None))
                {
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
                }
                Logger.LogInformation("Build canceled ({0})", buildId);
            }
            else if (engine is not null)
            {
                // the build was canceled, because of a server shutdown
                // switch state back to pending
                completionStatus = JobCompletionStatus.Restarting;
                await using (await @lock.WriterLockAsync(cancellationToken: CancellationToken.None))
                {
                    await DataAccessContext.WithTransactionAsync(
                        async (ct) =>
                        {
                            await PlatformService.BuildRestartingAsync(buildId, CancellationToken.None);
                            await BuildJobService.BuildJobRestartingAsync(engineId, buildId, CancellationToken.None);
                        },
                        cancellationToken: CancellationToken.None
                    );
                }
                throw;
            }
            else
            {
                completionStatus = JobCompletionStatus.Canceled;
            }
        }
        catch (Exception e)
        {
            completionStatus = JobCompletionStatus.Faulted;
            await using (await @lock.WriterLockAsync(cancellationToken: CancellationToken.None))
            {
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
            }
            Logger.LogError(0, e, "Build faulted ({0})", buildId);
            throw;
        }
        finally
        {
            await CleanupAsync(engineId, buildId, data, @lock, completionStatus);
        }
    }

    protected virtual Task InitializeAsync(
        string engineId,
        string buildId,
        T data,
        IDistributedReaderWriterLock @lock,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }

    protected abstract Task DoWorkAsync(
        string engineId,
        string buildId,
        T data,
        string? buildOptions,
        IDistributedReaderWriterLock @lock,
        CancellationToken cancellationToken
    );

    protected virtual Task CleanupAsync(
        string engineId,
        string buildId,
        T data,
        IDistributedReaderWriterLock @lock,
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
        Restarting
    }
}
