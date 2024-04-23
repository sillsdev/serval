namespace Serval.Aqua.Shared.Services;

public class PostprocessJobStage(
    IDataAccessContext dataAccessContext,
    IPlatformService platformService,
    IJobService jobService,
    ILogger<PostprocessJobStage> logger,
    IAquaService aquaService
) : HangfireJobStage<int>(dataAccessContext, platformService, jobService, logger)
{
    private readonly IAquaService _aquaService = aquaService;

    protected override async Task DoWorkAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        int assessmentId,
        string? jobOptions,
        CancellationToken cancellationToken
    )
    {
        Job? job = await JobService.GetAsync(jobId, cancellationToken);
        if (job is null)
            throw new OperationCanceledException();

        IReadOnlyList<ResultDto> results = await _aquaService.GetResultsAsync(
            assessmentId,
            cancellationToken: cancellationToken
        );
        await PlatformService.InsertResultsAsync(
            jobId,
            results.Where(r => IsIncluded(job.CorpusFilter, r)).Select(r => Map(jobId, r)),
            cancellationToken
        );

        await DataAccessContext.WithTransactionAsync(
            async ct =>
            {
                await JobService.StageFinishedAsync(jobId, ct);
                await PlatformService.JobCompletedAsync(jobId, ct);
            },
            CancellationToken.None
        );

        Logger.LogInformation("Job completed ({0}).", jobId);
    }

    protected override async Task CleanupAsync(
        string engineId,
        string jobId,
        AssessmentType assessmentType,
        int assessmentId,
        StageCompletionStatus completionStatus
    )
    {
        if (completionStatus is StageCompletionStatus.Restarting)
            return;

        try
        {
            await _aquaService.DeleteAssessmentAsync(assessmentId);
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Unable to to delete the assessment for job {0}.", jobId);
        }
    }

    private static bool IsIncluded(CorpusFilter filter, ResultDto result)
    {
        if (filter.IncludeAll)
            return true;

        VerseRef vref = new(result.Vref);
        if (filter.IncludeChapters is not null)
        {
            if (IsInChapters(filter.IncludeChapters, vref))
                return true;
        }
        return filter.IncludeTextIds.Contains(vref.Book);
    }

    private static bool IsInChapters(IReadOnlyDictionary<string, HashSet<int>> bookChapters, VerseRef vref)
    {
        return bookChapters.TryGetValue(vref.Book, out HashSet<int>? chapters)
            && (chapters.Contains(vref.ChapterNum) || chapters.Count == 0);
    }

    private static Result Map(string jobIdId, ResultDto source)
    {
        var vref = new VerseRef(source.Vref);
        return new Result
        {
            JobRef = jobIdId,
            TextId = vref.Book,
            Ref = source.Vref ?? "",
            Score = source.Score,
            Description = source.Note
        };
    }
}
