namespace Serval.Machine.Shared.Services;

public abstract class PostprocessBuildJob<TEngine>(
    IPlatformService platformService,
    IRepository<TEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<TEngine> buildJobService,
    ILogger<PostprocessBuildJob<TEngine>> logger,
    ISharedFileService sharedFileService,
    IOptionsMonitor<BuildJobOptions> options
) : HangfireBuildJob<TEngine, (int, double)>(platformService, engines, dataAccessContext, buildJobService, logger)
    where TEngine : ITrainingEngine
{
    protected ISharedFileService SharedFileService { get; } = sharedFileService;
    private readonly BuildJobOptions _buildJobOptions = options.CurrentValue;

    protected virtual Task<int> SaveModelAsync(string engineId, string buildId)
    {
        return Task.FromResult(0);
    }

    protected override async Task CleanupAsync(
        string engineId,
        string buildId,
        (int, double) data,
        JobCompletionStatus completionStatus
    )
    {
        if (completionStatus is JobCompletionStatus.Restarting)
            return;

        if (_buildJobOptions.PreserveBuildFiles)
            return;

        try
        {
            if (completionStatus is not JobCompletionStatus.Faulted)
                await SharedFileService.DeleteAsync($"builds/{buildId}/");
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Unable to to delete job data for build {0}.", buildId);
        }
    }
}
