namespace Serval.Machine.Shared.Services;

public class BuildJobService<TEngine>(IEnumerable<IBuildJobRunner> runners, IRepository<TEngine> engines)
    : IBuildJobService<TEngine>
    where TEngine : ITrainingEngine
{
    protected readonly Dictionary<BuildJobRunnerType, IBuildJobRunner> Runners = runners.ToDictionary(r => r.Type);
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

    public async Task CreateEngineAsync(
        string engineId,
        string? name = null,
        CancellationToken cancellationToken = default
    )
    {
        foreach (BuildJobRunnerType runnerType in Runners.Keys)
        {
            IBuildJobRunner runner = Runners[runnerType];
            await runner.CreateEngineAsync(engineId, name, cancellationToken);
        }
    }

    public async Task DeleteEngineAsync(string engineId, CancellationToken cancellationToken = default)
    {
        foreach (BuildJobRunnerType runnerType in Runners.Keys)
        {
            IBuildJobRunner runner = Runners[runnerType];
            await runner.DeleteEngineAsync(engineId, cancellationToken);
        }
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
        IBuildJobRunner runner = Runners[runnerType];
        string jobId = await runner.CreateJobAsync(
            engineType,
            engineId,
            buildId,
            stage,
            data,
            buildOptions,
            cancellationToken
        );
        try
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
                            JobId = jobId,
                            BuildJobRunner = runner.Type,
                            Stage = stage,
                            JobState = BuildJobState.Pending,
                            Options = buildOptions
                        }
                    ),
                cancellationToken: cancellationToken
            );
            if (engine is null)
            {
                await runner.DeleteJobAsync(jobId, CancellationToken.None);
                return false;
            }
            await runner.EnqueueJobAsync(jobId, engine.Type, cancellationToken);
            return true;
        }
        catch
        {
            await runner.DeleteJobAsync(jobId, CancellationToken.None);
            throw;
        }
    }

    public virtual async Task<(string? BuildId, BuildJobState State)> CancelBuildJobAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        // cancel a job that hasn't started yet
        TEngine? engine = await Engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.JobState == BuildJobState.Pending,
            u =>
            {
                u.Unset(b => b.CurrentBuild);
            },
            returnOriginal: true,
            cancellationToken: cancellationToken
        );
        if (engine is not null && engine.CurrentBuild is not null)
        {
            // job will be deleted from the queue
            IBuildJobRunner runner = Runners[engine.CurrentBuild.BuildJobRunner];
            await runner.StopJobAsync(engine.CurrentBuild.JobId, CancellationToken.None);
            return (engine.CurrentBuild.BuildId, BuildJobState.None);
        }

        // cancel a job that is already running
        engine = await Engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.JobState == BuildJobState.Active,
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Canceling),
            cancellationToken: cancellationToken
        );
        if (engine is not null && engine.CurrentBuild is not null)
        {
            IBuildJobRunner runner = Runners[engine.CurrentBuild.BuildJobRunner];
            await runner.StopJobAsync(engine.CurrentBuild.JobId, CancellationToken.None);
            return (engine.CurrentBuild.BuildId, BuildJobState.Canceling);
        }

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
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Pending),
            cancellationToken: cancellationToken
        );
    }
}
