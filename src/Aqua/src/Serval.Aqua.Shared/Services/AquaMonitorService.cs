namespace Serval.Aqua.Shared.Services;

public class AquaMonitorService(
    IServiceProvider services,
    IAquaService aquaService,
    IOptions<AquaOptions> options,
    ILogger<AquaMonitorService> logger
)
    : RecurrentTask(
        "AQuA monitor service",
        services,
        options.Value.JobPollingTimeout,
        logger,
        options.Value.JobPollingEnabled
    )
{
    private readonly IAquaService _aquaService = aquaService;
    private readonly ILogger<AquaMonitorService> _logger = logger;

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
            IReadOnlyList<Job> jobs = await jobService.GetActiveJobsAsync(JobStage.Assess, cancellationToken);
            if (jobs.Count == 0)
                return;

            Dictionary<string, AssessmentDto> assessments = (
                await _aquaService.GetAssessmentsAsync(cancellationToken)
            ).ToDictionary(a => a.Id.ToString(CultureInfo.InvariantCulture));

            var platformService = scope.ServiceProvider.GetRequiredService<IPlatformService>();
            var dataAccessContext = scope.ServiceProvider.GetRequiredService<IDataAccessContext>();
            foreach (Job job in jobs)
            {
                if (job.StageId is null || !assessments.TryGetValue(job.StageId, out AssessmentDto? assessment))
                    continue;
                assessments.Remove(job.StageId);

                bool canceling = false;
                if (job.StageState is JobStageState.Pending && assessment.Status is not AssessmentStatus.Queued)
                {
                    canceling = !await JobStartedAsync(
                        jobService,
                        platformService,
                        dataAccessContext,
                        job.Id,
                        cancellationToken
                    );
                }

                if (canceling || job.StageState is JobStageState.Canceling)
                {
                    canceling = true;
                }
                else
                {
                    switch (assessment.Status)
                    {
                        case AssessmentStatus.Finished:
                            canceling = !await jobService.StartPostprocessStageAsync(
                                job.EngineRef,
                                job.Id,
                                assessment.Type,
                                assessment.Id,
                                job.Options,
                                cancellationToken
                            );
                            break;

                        case AssessmentStatus.Failed:
                            await JobFaultedAsync(
                                jobService,
                                platformService,
                                dataAccessContext,
                                job.Id,
                                assessment.Id,
                                "The AQuA assessment failed.",
                                cancellationToken
                            );
                            break;
                    }
                }

                if (canceling)
                {
                    await JobCanceledAsync(
                        jobService,
                        platformService,
                        dataAccessContext,
                        job.Id,
                        assessment.Id,
                        cancellationToken
                    );
                }
            }

            foreach (AssessmentDto assessment in assessments.Values)
                await _aquaService.DeleteAssessmentAsync(assessment.Id, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while monitoring AQuA assessments.");
        }
    }

    private Task<bool> JobStartedAsync(
        IJobService jobService,
        IPlatformService platformService,
        IDataAccessContext dataAccessContext,
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        return dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                if (!await jobService.StageStartedAsync(jobId, ct))
                    return false;
                await platformService.JobStartedAsync(jobId, ct);
                _logger.LogInformation("Job started ({0})", jobId);
                return true;
            },
            cancellationToken
        );
    }

    private async Task JobFaultedAsync(
        IJobService jobService,
        IPlatformService platformService,
        IDataAccessContext dataAccessContext,
        string jobId,
        int assessmentId,
        string message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await dataAccessContext.WithTransactionAsync(
                async ct =>
                {
                    await jobService.StageFinishedAsync(jobId, ct);
                    await platformService.JobFaultedAsync(jobId, message, ct);
                    _logger.LogError("Job faulted ({0}). Error: {1}", jobId, message);
                },
                cancellationToken
            );
        }
        finally
        {
            try
            {
                await _aquaService.DeleteAssessmentAsync(assessmentId, CancellationToken.None);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to to delete the assessment for job {0}.", jobId);
            }
        }
    }

    private async Task JobCanceledAsync(
        IJobService jobService,
        IPlatformService platformService,
        IDataAccessContext dataAccessContext,
        string jobId,
        int assessmentId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await dataAccessContext.WithTransactionAsync(
                async ct =>
                {
                    await jobService.StageFinishedAsync(jobId, ct);
                    await platformService.JobCanceledAsync(jobId, ct);
                    _logger.LogInformation("JOb canceled ({0})", jobId);
                },
                cancellationToken
            );
        }
        finally
        {
            try
            {
                await _aquaService.DeleteAssessmentAsync(assessmentId, CancellationToken.None);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to to delete the assessment for job {0}.", jobId);
            }
        }
    }
}
