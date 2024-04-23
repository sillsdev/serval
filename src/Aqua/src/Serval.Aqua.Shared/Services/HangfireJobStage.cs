namespace Serval.Aqua.Shared.Services;

public abstract class HangfireJobStage(
    IDataAccessContext dataAccessContext,
    IPlatformService platformService,
    IJobService jobService,
    ILogger<HangfireJobStage> logger
) : HangfireJobStage<object?>(dataAccessContext, platformService, jobService, logger)
{
    public virtual Task RunAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        string? jobOptions,
        CancellationToken cancellationToken
    )
    {
        return RunAsync(engineId, jobId, assessmentType, null, jobOptions, cancellationToken);
    }
}

public abstract class HangfireJobStage<T>(
    IDataAccessContext dataAccessContext,
    IPlatformService platformService,
    IJobService jobService,
    ILogger<HangfireJobStage<T>> logger
)
{
    protected IDataAccessContext DataAccessContext { get; } = dataAccessContext;
    protected IPlatformService PlatformService { get; } = platformService;
    protected IJobService JobService { get; } = jobService;
    protected ILogger<HangfireJobStage<T>> Logger { get; } = logger;

    public virtual async Task RunAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        T data,
        string? jobOptions,
        CancellationToken cancellationToken
    )
    {
        StageCompletionStatus completionStatus = StageCompletionStatus.Completed;
        try
        {
            await InitializeAsync(engineId, jobId, assessmentType, data, cancellationToken);
            if (!await JobService.StageStartedAsync(jobId, cancellationToken))
            {
                completionStatus = StageCompletionStatus.Canceled;
                return;
            }

            await DoWorkAsync(engineId, jobId, assessmentType, data, jobOptions, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Check if the cancellation was initiated by an API call or a shutdown.
            bool? canceling = await JobService.IsCancelingAsync(jobId, CancellationToken.None);
            if (canceling is null)
            {
                completionStatus = StageCompletionStatus.Canceled;
            }
            else if (canceling.Value)
            {
                completionStatus = StageCompletionStatus.Canceled;
                await DataAccessContext.WithTransactionAsync(
                    async ct =>
                    {
                        await PlatformService.JobCanceledAsync(jobId, ct);
                        await JobService.StageFinishedAsync(jobId, ct);
                    },
                    CancellationToken.None
                );
                Logger.LogInformation("Job canceled ({0})", jobId);
            }
            else
            {
                // the job was canceled, because of a server shutdown
                // switch state back to pending
                completionStatus = StageCompletionStatus.Restarting;
                await DataAccessContext.WithTransactionAsync(
                    async ct =>
                    {
                        await PlatformService.JobRestartingAsync(jobId, ct);
                        await JobService.StageRestartingAsync(jobId, ct);
                    },
                    CancellationToken.None
                );
                throw;
            }
        }
        catch (Exception e)
        {
            completionStatus = StageCompletionStatus.Faulted;
            await DataAccessContext.WithTransactionAsync(
                async ct =>
                {
                    await PlatformService.JobFaultedAsync(jobId, e.Message, ct);
                    await JobService.StageFinishedAsync(jobId, ct);
                },
                CancellationToken.None
            );
            Logger.LogError(0, e, "Job faulted ({0})", jobId);
            throw;
        }
        finally
        {
            await CleanupAsync(engineId, jobId, assessmentType, data, completionStatus);
        }
    }

    protected virtual Task InitializeAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        T data,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }

    protected abstract Task DoWorkAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        T data,
        string? jobOptions,
        CancellationToken cancellationToken
    );

    protected virtual Task CleanupAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        T data,
        StageCompletionStatus completionStatus
    )
    {
        return Task.CompletedTask;
    }

    protected enum StageCompletionStatus
    {
        Completed,
        Faulted,
        Canceled,
        Restarting
    }
}
