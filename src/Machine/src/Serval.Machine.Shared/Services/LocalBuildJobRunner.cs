namespace Serval.Machine.Shared.Services;

public class LocalBuildJobRunner(
    IEnumerable<ILocalBuildJobFactory> factories,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<LocalBuildJobRunner> logger
) : BackgroundService, IBuildJobRunner
{
    private static readonly Dictionary<EngineType, EngineGroup> EngineGroups = new()
    {
        [EngineType.SmtTransfer] = EngineGroup.Translation,
        [EngineType.Nmt] = EngineGroup.Translation,
        [EngineType.Statistical] = EngineGroup.WordAlignment,
    };

    private static readonly BoundedChannelOptions ChannelOptions = new(128)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false,
    };

    private readonly Dictionary<EngineGroup, Channel<string>> _jobChannels = new()
    {
        [EngineGroup.Translation] = Channel.CreateBounded<string>(ChannelOptions),
        [EngineGroup.WordAlignment] = Channel.CreateBounded<string>(ChannelOptions),
    };
    private readonly ConcurrentDictionary<string, JobInfo> _pendingJobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCts = new();
    private readonly Dictionary<EngineType, ILocalBuildJobFactory> _factories = factories.ToDictionary(f =>
        f.EngineType
    );
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger<LocalBuildJobRunner> _logger = logger;

    public BuildJobRunnerType Type => BuildJobRunnerType.Local;

    public Task CreateEngineAsync(
        string engineId,
        string? name = null,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    public Task DeleteEngineAsync(string engineId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<(string JobId, string? JobData)> CreateJobAsync(
        EngineType engineType,
        string engineId,
        string buildId,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        string jobId = Guid.NewGuid().ToString();
        string? jobData = _factories.TryGetValue(engineType, out ILocalBuildJobFactory? factory)
            ? factory.Serialize(stage, data)
            : null;
        return Task.FromResult((jobId, jobData));
    }

    public Task<bool> DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        bool removed = _pendingJobs.TryRemove(jobId, out _);
        if (_activeCts.TryRemove(jobId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
            removed = true;
        }
        return Task.FromResult(removed);
    }

    public Task<bool> EnqueueJobAsync(
        string jobId,
        EngineType engineType,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(true);

    public Task<bool> StopJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _pendingJobs.TryRemove(jobId, out _);
        if (_activeCts.TryRemove(jobId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Scope lives for the duration of ExecuteAsync to keep subscriptions alive.
        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        var translationEngines = scope.ServiceProvider.GetRequiredService<IRepository<TranslationEngine>>();
        var wordAlignmentEngines = scope.ServiceProvider.GetRequiredService<IRepository<WordAlignmentEngine>>();

        // Subscriptions are created before recovery so no changes are missed during the recovery window.
        using ISubscription<TranslationEngine> translationSub = await translationEngines.SubscribeAsync(
            e =>
                e.CurrentBuild != null
                && e.CurrentBuild.BuildJobRunner == BuildJobRunnerType.Local
                && e.CurrentBuild.JobState == BuildJobState.Pending,
            changeTypes: new HashSet<EntityChangeType> { EntityChangeType.Insert, EntityChangeType.Update },
            cancellationToken: stoppingToken
        );
        using ISubscription<WordAlignmentEngine> wordAlignmentSub = await wordAlignmentEngines.SubscribeAsync(
            e =>
                e.CurrentBuild != null
                && e.CurrentBuild.BuildJobRunner == BuildJobRunnerType.Local
                && e.CurrentBuild.JobState == BuildJobState.Pending,
            changeTypes: new HashSet<EntityChangeType> { EntityChangeType.Insert, EntityChangeType.Update },
            cancellationToken: stoppingToken
        );

        await RecoverPendingJobsAsync(scope.ServiceProvider, stoppingToken);

        await Task.WhenAll(
            WatchEngineGroupAsync(translationSub, EngineGroup.Translation, stoppingToken),
            WatchEngineGroupAsync(wordAlignmentSub, EngineGroup.WordAlignment, stoppingToken),
            ProcessJobsAsync(EngineGroup.Translation, stoppingToken),
            ProcessJobsAsync(EngineGroup.WordAlignment, stoppingToken)
        );
    }

    private async Task RecoverPendingJobsAsync(IServiceProvider sp, CancellationToken cancellationToken)
    {
        var translationBuildJobService = sp.GetRequiredService<IBuildJobService<TranslationEngine>>();
        var wordAlignmentBuildJobService = sp.GetRequiredService<IBuildJobService<WordAlignmentEngine>>();
        var dataAccessContext = sp.GetRequiredService<IDataAccessContext>();
        var translationPlatform = sp.GetRequiredKeyedService<IPlatformService>(EngineGroup.Translation);
        var wordAlignmentPlatform = sp.GetRequiredKeyedService<IPlatformService>(EngineGroup.WordAlignment);

        await RecoverEngineGroupAsync(
            translationBuildJobService,
            translationPlatform,
            dataAccessContext,
            cancellationToken
        );
        await RecoverEngineGroupAsync(
            wordAlignmentBuildJobService,
            wordAlignmentPlatform,
            dataAccessContext,
            cancellationToken
        );
    }

    private async Task RecoverEngineGroupAsync<TEngine>(
        IBuildJobService<TEngine> buildJobService,
        IPlatformService platformService,
        IDataAccessContext dataAccessContext,
        CancellationToken cancellationToken
    )
        where TEngine : ITrainingEngine
    {
        IReadOnlyList<TEngine> engines = await buildJobService.GetBuildingEnginesAsync(
            BuildJobRunnerType.Local,
            cancellationToken
        );

        foreach (TEngine engine in engines.Where(e => e.CurrentBuild!.JobState == BuildJobState.Active))
        {
            await ResetActiveJobAsync(
                buildJobService,
                platformService,
                dataAccessContext,
                engine.EngineId,
                engine.CurrentBuild!.BuildId,
                cancellationToken
            );
        }

        // Re-query after Active→Pending resets to get the refreshed list
        IReadOnlyList<TEngine> pending = await buildJobService.GetBuildingEnginesAsync(
            BuildJobRunnerType.Local,
            cancellationToken
        );

        foreach (
            TEngine engine in pending
                .Where(e => e.CurrentBuild!.JobState == BuildJobState.Pending)
                .OrderBy(e => e.CurrentBuild!.QueuedAt)
        )
        {
            EnqueueRecoveredJob(engine.EngineId, engine.CurrentBuild!, engine.Type);
        }
    }

    private static async Task ResetActiveJobAsync(
        IBuildJobService buildJobService,
        IPlatformService platformService,
        IDataAccessContext dataAccessContext,
        string engineId,
        string buildId,
        CancellationToken cancellationToken
    )
    {
        await dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                await platformService.BuildRestartingAsync(buildId, CancellationToken.None);
                await buildJobService.BuildJobRestartingAsync(engineId, buildId, CancellationToken.None);
            },
            cancellationToken: cancellationToken
        );
    }

    private void EnqueueRecoveredJob(string engineId, Build build, EngineType engineType)
    {
        if (
            _pendingJobs.TryAdd(
                build.JobId,
                new JobInfo(engineId, build.BuildId, engineType, build.Stage, build.JobData, build.Options)
            )
        )
        {
            _jobChannels[EngineGroups[engineType]].Writer.TryWrite(build.JobId);
        }
    }

    private async Task WatchEngineGroupAsync<TEngine>(
        ISubscription<TEngine> subscription,
        EngineGroup engineGroup,
        CancellationToken cancellationToken
    )
        where TEngine : ITrainingEngine
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                EntityChange<TEngine> change = subscription.Change;
                if (change.Type is EntityChangeType.Insert or EntityChangeType.Update && change.Entity != null)
                {
                    TEngine engine = change.Entity;
                    Build? build = engine.CurrentBuild;
                    if (
                        build?.BuildJobRunner == BuildJobRunnerType.Local
                        && build.JobState == BuildJobState.Pending
                        && !_activeCts.ContainsKey(build.JobId)
                        && _pendingJobs.TryAdd(
                            build.JobId,
                            new JobInfo(
                                engine.EngineId,
                                build.BuildId,
                                engine.Type,
                                build.Stage,
                                build.JobData,
                                build.Options
                            )
                        )
                    )
                    {
                        _jobChannels[engineGroup].Writer.TryWrite(build.JobId);
                    }
                }
                await subscription.WaitForChangeAsync(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while watching {EngineGroup} engines.", engineGroup);
                throw;
            }
        }
    }

    private async Task ProcessJobsAsync(EngineGroup engineGroup, CancellationToken stoppingToken)
    {
        Channel<string> channel = _jobChannels[engineGroup];
        while (!stoppingToken.IsCancellationRequested)
        {
            string jobId;
            try
            {
                jobId = await channel.Reader.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_pendingJobs.TryRemove(jobId, out JobInfo? info))
                continue;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _activeCts[jobId] = cts;
            try
            {
                await ExecuteJobAsync(jobId, info, cts);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // job was explicitly canceled via StopJobAsync; continue processing the queue
            }
        }
    }

    private async Task ExecuteJobAsync(string jobId, JobInfo info, CancellationTokenSource cts)
    {
        try
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            ILocalBuildJobFactory factory = _factories[info.EngineType];
            await factory.RunAsync(
                scope.ServiceProvider,
                info.EngineId,
                info.BuildId,
                info.Stage,
                info.JobData,
                info.BuildOptions,
                cts.Token
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unhandled exception in local build job {JobId}", jobId);
        }
        finally
        {
            _activeCts.TryRemove(jobId, out _);
            cts.Dispose();
        }
    }

    private record JobInfo(
        string EngineId,
        string BuildId,
        EngineType EngineType,
        BuildStage Stage,
        string? JobData,
        string? BuildOptions
    );
}
