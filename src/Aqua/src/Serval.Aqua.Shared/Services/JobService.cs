namespace Serval.Aqua.Shared.Services;

public class JobService(
    IDataAccessContext dataAccessContext,
    IRepository<Job> jobs,
    IBackgroundJobClient jobClient,
    IAquaService aquaService
) : IJobService
{
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<Job> _jobs = jobs;
    private readonly IBackgroundJobClient _jobClient = jobClient;
    private readonly IAquaService _aquaService = aquaService;

    public Task<Job?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _jobs.GetAsync(id, cancellationToken);
    }

    public async Task CreateAsync(
        string engineId,
        string id,
        AssessmentType assessmentType,
        JobData data,
        string? options,
        CorpusFilter corpusFilter,
        CancellationToken cancellationToken = default
    )
    {
        await _jobs.InsertAsync(
            new Job
            {
                Id = id,
                EngineRef = engineId,
                Options = options,
                CorpusFilter = corpusFilter
            },
            cancellationToken: cancellationToken
        );

        await StartHangfireStageAsync<PreprocessJobStage, JobData>(
            engineId,
            id,
            assessmentType,
            JobStage.Preprocess,
            data,
            options,
            cancellationToken
        );
    }

    public Task<bool> StartAssessmentStageAsync(
        string engineId,
        string id,
        AssessmentType assessmentType,
        int revisionId,
        int? referenceId = null,
        string? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                Job? job = await _jobs.UpdateAsync(
                    j => j.Id == id && j.StageState != JobStageState.Canceling,
                    u =>
                    {
                        u.Set(j => j.StageState, JobStageState.Pending);
                        u.Set(j => j.Stage, JobStage.Assess);
                    },
                    cancellationToken: ct
                );
                if (job is null)
                    return false;

                AssessmentDto assessment = await _aquaService.CreateAssessmentAsync(
                    assessmentType,
                    revisionId,
                    referenceId,
                    ct
                );
                try
                {
                    await _jobs.UpdateAsync(
                        id,
                        u => u.Set(j => j.StageId, assessment.Id.ToString(CultureInfo.InvariantCulture)),
                        cancellationToken: ct
                    );
                    return true;
                }
                catch
                {
                    await _aquaService.DeleteAssessmentAsync(assessment.Id, CancellationToken.None);
                    throw;
                }
            },
            cancellationToken
        );
    }

    public Task<bool> StartPostprocessStageAsync(
        string engineId,
        string id,
        AssessmentType assessmentType,
        int assessmentId,
        string? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return StartHangfireStageAsync<PostprocessJobStage, int>(
            engineId,
            id,
            assessmentType,
            JobStage.Postprocess,
            assessmentId,
            options,
            cancellationToken
        );
    }

    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        Job? job = await _jobs.UpdateAsync(
            j => j.Id == id && j.StageState != null,
            u => u.Set(j => j.StageState, JobStageState.Canceling),
            cancellationToken: cancellationToken
        );
        if (job is not null && job.Stage is JobStage.Preprocess or JobStage.Postprocess && job.StageId is not null)
        {
            _jobClient.Delete(job.StageId);
        }
    }

    public async Task<string?> GetStageIdAsync(string id, CancellationToken cancellationToken = default)
    {
        Job? job = await _jobs.GetAsync(id, cancellationToken);
        return job?.StageId;
    }

    public async Task<bool?> IsCancelingAsync(string id, CancellationToken cancellationToken = default)
    {
        Job? job = await _jobs.GetAsync(id, cancellationToken);
        if (job is null)
            return null;
        return job.StageState is JobStageState.Canceling;
    }

    public Task<IReadOnlyList<Job>> GetActiveJobsAsync(JobStage stage, CancellationToken cancellationToken = default)
    {
        return _jobs.GetAllAsync(j => j.Stage == stage, cancellationToken);
    }

    public async Task<bool> StageStartedAsync(string id, CancellationToken cancellationToken = default)
    {
        Job? job = await _jobs.UpdateAsync(
            j => j.Id == id && j.StageState == JobStageState.Pending,
            u => u.Set(j => j.StageState, JobStageState.Active),
            cancellationToken: cancellationToken
        );
        return job is not null;
    }

    public Task StageFinishedAsync(string id, CancellationToken cancellationToken = default)
    {
        return _jobs.UpdateAsync(
            j => j.Id == id && j.StageState != null,
            u =>
            {
                u.Unset(j => j.StageState);
                u.Unset(j => j.StageId);
                u.Unset(j => j.Stage);
            },
            cancellationToken: cancellationToken
        );
    }

    public Task StageRestartingAsync(string id, CancellationToken cancellationToken = default)
    {
        return _jobs.UpdateAsync(
            j => j.Id == id && j.StageState != null,
            j => j.Set(b => b.StageState, JobStageState.Pending),
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> StartHangfireStageAsync<TStage, TData>(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        JobStage stage,
        TData data,
        string? jobOptions,
        CancellationToken cancellationToken = default
    )
        where TStage : HangfireJobStage<TData>
    {
        string stageId = _jobClient.Schedule<TStage>(
            x => x.RunAsync(engineId, jobId, assessmentType, data, jobOptions, CancellationToken.None),
            TimeSpan.FromDays(10000)
        );
        try
        {
            Job? job = await _jobs.UpdateAsync(
                j => j.Id == jobId && j.StageState != JobStageState.Canceling,
                u =>
                {
                    u.Set(j => j.StageId, stageId);
                    u.Set(j => j.StageState, JobStageState.Pending);
                    u.Set(j => j.Stage, stage);
                },
                cancellationToken: cancellationToken
            );
            if (job is null)
                return false;
            _jobClient.Requeue(stageId);
            return true;
        }
        catch
        {
            _jobClient.Delete(stageId);
            throw;
        }
    }
}
