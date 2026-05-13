namespace Serval.Machine.Shared.Services;

public class TranslationBuildJobService(IRepository<TranslationEngine> engines)
    : BuildJobService<TranslationEngine>(engines)
{
    public override async Task<(string? BuildId, BuildJobState State)> CancelBuildJobAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        // cancel a job that hasn't started yet
        TranslationEngine? engine = await Engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.JobState == BuildJobState.Pending,
            u =>
            {
                u.Unset(b => b.CurrentBuild);
                u.Set(e => e.CollectTrainSegmentPairs, false);
            },
            returnOriginal: true,
            cancellationToken: cancellationToken
        );
        if (engine is not null && engine.CurrentBuild is not null)
        {
            return (engine.CurrentBuild.BuildId, BuildJobState.None);
        }

        // cancel a job that is already running or already created
        engine = await Engines.UpdateAsync(
            e =>
                e.EngineId == engineId
                && e.CurrentBuild != null
                && (
                    e.CurrentBuild.JobState == BuildJobState.Pending || e.CurrentBuild.JobState == BuildJobState.Active
                ),
            u =>
            {
                u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Canceling);
                u.Set(e => e.CollectTrainSegmentPairs, false);
            },
            cancellationToken: cancellationToken
        );
        if (engine is not null && engine.CurrentBuild is not null)
        {
            return (engine.CurrentBuild.BuildId, BuildJobState.Canceling);
        }

        return (null, BuildJobState.None);
    }

    public override Task BuildJobFinishedAsync(
        string engineId,
        string buildId,
        bool buildComplete,
        CancellationToken cancellationToken = default
    )
    {
        return Engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.BuildId == buildId,
            u =>
            {
                u.Unset(e => e.CurrentBuild);
                u.Set(e => e.CollectTrainSegmentPairs, false);
                if (buildComplete)
                    u.Inc(e => e.BuildRevision);
            },
            cancellationToken: cancellationToken
        );
    }
}
