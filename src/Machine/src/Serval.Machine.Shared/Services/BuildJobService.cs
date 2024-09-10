namespace Serval.Machine.Shared.Services;

public class BuildJobService(IEnumerable<IBuildJobRunner> runners, IRepository<TranslationEngine> engines)
    : IBuildJobService
{
    private readonly Dictionary<BuildJobRunnerType, IBuildJobRunner> _runners = runners.ToDictionary(r => r.Type);
    private readonly IRepository<TranslationEngine> _engines = engines;

    public Task<bool> IsEngineBuilding(string engineId, CancellationToken cancellationToken = default)
    {
        return _engines.ExistsAsync(e => e.EngineId == engineId && e.CurrentBuild != null, cancellationToken);
    }

    public Task<IReadOnlyList<TranslationEngine>> GetBuildingEnginesAsync(
        BuildJobRunnerType runner,
        CancellationToken cancellationToken = default
    )
    {
        return _engines.GetAllAsync(
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
        TranslationEngine? engine = await _engines.GetAsync(
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
        foreach (BuildJobRunnerType runnerType in _runners.Keys)
        {
            IBuildJobRunner runner = _runners[runnerType];
            await runner.CreateEngineAsync(engineId, name, cancellationToken);
        }
    }

    public async Task DeleteEngineAsync(string engineId, CancellationToken cancellationToken = default)
    {
        foreach (BuildJobRunnerType runnerType in _runners.Keys)
        {
            IBuildJobRunner runner = _runners[runnerType];
            await runner.DeleteEngineAsync(engineId, cancellationToken);
        }
    }

    public async Task<bool> StartBuildJobAsync(
        BuildJobRunnerType runnerType,
        TranslationEngineType engineType,
        string engineId,
        string buildId,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        IBuildJobRunner runner = _runners[runnerType];
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
            TranslationEngine? engine = await _engines.UpdateAsync(
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

    public async Task<(string? BuildId, BuildJobState State)> CancelBuildJobAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        // cancel a job that hasn't started yet
        TranslationEngine? engine = await _engines.UpdateAsync(
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
            // job will be deleted from the queue
            IBuildJobRunner runner = _runners[engine.CurrentBuild.BuildJobRunner];
            await runner.StopJobAsync(engine.CurrentBuild.JobId, CancellationToken.None);
            return (engine.CurrentBuild.BuildId, BuildJobState.None);
        }

        // cancel a job that is already running
        engine = await _engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.JobState == BuildJobState.Active,
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Canceling),
            cancellationToken: cancellationToken
        );
        if (engine is not null && engine.CurrentBuild is not null)
        {
            IBuildJobRunner runner = _runners[engine.CurrentBuild.BuildJobRunner];
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
        TranslationEngine? engine = await _engines.UpdateAsync(
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

    public Task BuildJobFinishedAsync(
        string engineId,
        string buildId,
        bool buildComplete,
        CancellationToken cancellationToken = default
    )
    {
        return _engines.UpdateAsync(
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

    public Task BuildJobRestartingAsync(string engineId, string buildId, CancellationToken cancellationToken = default)
    {
        return _engines.UpdateAsync(
            e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.BuildId == buildId,
            u => u.Set(e => e.CurrentBuild!.JobState, BuildJobState.Pending),
            cancellationToken: cancellationToken
        );
    }
}
