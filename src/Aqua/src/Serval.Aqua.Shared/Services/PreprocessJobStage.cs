namespace Serval.Aqua.Shared.Services;

public class PreprocessJobStage(
    IDataAccessContext dataAccessContext,
    IPlatformService platformService,
    IJobService jobService,
    ILogger<PreprocessJobStage> logger,
    ICorpusService corpusService
) : HangfireJobStage<JobData>(dataAccessContext, platformService, jobService, logger)
{
    private readonly ICorpusService _corpusService = corpusService;

    protected override async Task DoWorkAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        JobData jobData,
        string? jobOptions,
        CancellationToken cancellationToken
    )
    {
        int revisionId = await _corpusService.CreateRevisionAsync(jobData.CorpusData, cancellationToken);
        int? referenceId = null;
        if (jobData.ReferenceCorpusData is not null)
            referenceId = await _corpusService.CreateRevisionAsync(jobData.ReferenceCorpusData, cancellationToken);

        if (
            !await JobService.StartAssessmentStageAsync(
                engineId,
                jobId,
                assessmentType,
                revisionId,
                referenceId,
                jobOptions,
                cancellationToken
            )
        )
        {
            throw new OperationCanceledException();
        }
    }
}
