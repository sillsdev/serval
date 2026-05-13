namespace Serval.Machine.Shared.Services;

public class BuildJobService<TEngine>(IRepository<TEngine> engines) : IBuildJobService<TEngine>
    where TEngine : ITrainingEngine
{
    protected readonly IRepository<TEngine> Engines = engines;

    public Task<bool> IsEngineBuilding(string engineId, CancellationToken cancellationToken = default)
    {
        return Engines.ExistsAsync(e => e.EngineId == engineId && e.CurrentBuild != null, cancellationToken);
    }

    public async Task<IReadOnlyList<TEngine>> GetBuildingEnginesAsync(
        BuildJobRunnerType runner,
        CancellationToken cancellationToken = default
    )
    {
        return await Engines.GetAllAsync(
            e => e.CurrentBuild != null && e.CurrentBuild.BuildJobRunner == runner,
            cancellationToken
        );
    }

    public async Task<Build?> GetBuildAsync(
        string engineId,
        string buildId,
        CancellationToken cancellationToken = default
    )
    {
        TEngine? engine = await Engines.GetAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.BuildId == buildId,
            cancellationToken
        );
        return engine?.CurrentBuild;
    }

    public Task DeleteEngineAsync(string engineId, CancellationToken cancellationToken = default)
    {
        return Engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && (e.CurrentBuild.JobState == BuildJobState.Active),
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Deleting),
            cancellationToken: cancellationToken
        );
    }

    public async Task<bool> StartBuildJobAsync(
        BuildJobRunnerType runnerType,
        EngineType engineType,
        string engineId,
        string buildId,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        TEngine? engine = await Engines.UpdateAsync(
            e =>
                e.EngineId == engineId
                && (
                    (stage == BuildStage.Preprocess && e.CurrentBuild == null)
                    || (
                        stage != BuildStage.Preprocess
                        && e.CurrentBuild != null
                        && e.CurrentBuild.JobState != BuildJobState.Canceling
                    )
                ),
            u =>
                u.Set(
                    e => e.CurrentBuild,
                    new Build
                    {
                        BuildId = buildId,
                        JobId = null,
                        BuildJobRunner = runnerType,
                        Stage = stage,
                        JobState = BuildJobState.Queued,
                        Options = buildOptions,
                        Data = data,
                        ExecutionData = new BuildExecutionData(),
                    }
                ),
            cancellationToken: cancellationToken
        );

        return engine is not null;
    }

    public virtual async Task<(string? BuildId, BuildJobState State)> CancelBuildJobAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        // cancel a job that hasn't started yet
        TEngine? engine = await Engines.UpdateAsync(
            e =>
                e.EngineId == engineId
                && e.CurrentBuild != null
                && (e.CurrentBuild.JobState == BuildJobState.Pending || e.CurrentBuild.JobState == BuildJobState.Queued)
                && e.CurrentBuild.JobId == null,
            u =>
            {
                u.Unset(b => b.CurrentBuild);
            },
            returnOriginal: true,
            cancellationToken: cancellationToken
        );
        if (engine is not null && engine.CurrentBuild is not null)
            return (engine.CurrentBuild.BuildId, BuildJobState.None);

        // mark a job that is already running as canceling and the dispatcher will stop it
        engine = await Engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && (e.CurrentBuild.JobState == BuildJobState.Active),
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Canceling),
            cancellationToken: cancellationToken
        );
        if (engine is not null && engine.CurrentBuild is not null)
            return (engine.CurrentBuild.BuildId, BuildJobState.Canceling);

        return (null, BuildJobState.None);
    }

    public async Task<bool> BuildJobStartedAsync(
        string engineId,
        string buildId,
        CancellationToken cancellationToken = default
    )
    {
        TEngine? engine = await Engines.UpdateAsync(
            e =>
                e.EngineId == engineId
                && e.CurrentBuild != null
                && e.CurrentBuild.BuildId == buildId
                && e.CurrentBuild.JobState == BuildJobState.Pending,
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Active),
            cancellationToken: cancellationToken
        );
        return engine is not null;
    }

    public virtual Task BuildJobFinishedAsync(
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
                if (buildComplete)
                    u.Inc(e => e.BuildRevision);
            },
            cancellationToken: cancellationToken
        );
    }

    public Task BuildJobRestartingAsync(string engineId, string buildId, CancellationToken cancellationToken = default)
    {
        return Engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.BuildId == buildId,
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Queued),
            cancellationToken: cancellationToken
        );
    }
}
