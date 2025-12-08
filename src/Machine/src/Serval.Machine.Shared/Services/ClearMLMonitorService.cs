namespace Serval.Machine.Shared.Services;

public class ClearMLMonitorService(
    IServiceProvider services,
    IClearMLService clearMLService,
    ISharedFileService sharedFileService,
    IOptionsMonitor<ClearMLOptions> clearMLOptions,
    IOptionsMonitor<BuildJobOptions> buildJobOptions,
    ILogger<ClearMLMonitorService> logger
)
    : RecurrentTask(
        "ClearML monitor service",
        services,
        clearMLOptions.CurrentValue.BuildPollingTimeout,
        logger,
        clearMLOptions.CurrentValue.BuildPollingEnabled
    ),
        IClearMLQueueService
{
    internal const string UserProperties = "properties";
    internal static readonly string SummaryMetric = CreateMD5("Summary");
    internal static readonly string TrainCorpusSizeVariant = CreateMD5("train_corpus_size");
    internal static readonly string ConfidenceVariant = CreateMD5("confidence");

    private readonly IClearMLService _clearMLService = clearMLService;
    private readonly ISharedFileService _sharedFileService = sharedFileService;
    private readonly ILogger<IClearMLQueueService> _logger = logger;
    private readonly Dictionary<string, ProgressStatus> _curBuildStatus = new();

    private readonly IReadOnlyDictionary<EngineType, string> _queuePerEngineType =
        buildJobOptions.CurrentValue.ClearML.ToDictionary(x => x.EngineType, x => x.Queue);

    private readonly IDictionary<EngineType, int> _queueSizePerEngineType = new ConcurrentDictionary<EngineType, int>(
        buildJobOptions.CurrentValue.ClearML.ToDictionary(x => x.EngineType, x => 0)
    );

    public int GetQueueSize(EngineType engineType)
    {
        return _queueSizePerEngineType[engineType];
    }

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        await MonitorClearMLTasksPerDomain(scope, cancellationToken);
    }

    internal async Task MonitorClearMLTasksPerDomain(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var translationBuildJobService = scope.ServiceProvider.GetRequiredService<
                IBuildJobService<TranslationEngine>
            >();
            var wordAlignmentBuildJobService = scope.ServiceProvider.GetRequiredService<
                IBuildJobService<WordAlignmentEngine>
            >();

            Dictionary<ITrainingEngine, IBuildJobService> engineToBuildServiceDict = (
                await translationBuildJobService.GetBuildingEnginesAsync(BuildJobRunnerType.ClearML, cancellationToken)
            ).ToDictionary(e => (ITrainingEngine)e, e => (IBuildJobService)translationBuildJobService);

            foreach (
                var engine in await wordAlignmentBuildJobService.GetBuildingEnginesAsync(
                    BuildJobRunnerType.ClearML,
                    cancellationToken
                )
            )
            {
                engineToBuildServiceDict[engine] = wordAlignmentBuildJobService;
            }

            if (engineToBuildServiceDict.Count == 0)
                return;

            Dictionary<string, ClearMLTask> tasks = (
                await _clearMLService.GetTasksByIdAsync(
                    engineToBuildServiceDict.Select(e => e.Key.CurrentBuild!.JobId),
                    cancellationToken
                )
            ).ToDictionary(t => t.Id);
            Dictionary<EngineType, Dictionary<string, int>> queuePositionsPerEngineType = new();

            foreach ((EngineType engineType, string queueName) in _queuePerEngineType)
            {
                var tasksPerEngineType = tasks
                    .Where(kvp =>
                        engineToBuildServiceDict
                            .Where(te => te.Key.CurrentBuild?.JobId == kvp.Key)
                            .FirstOrDefault()
                            .Key?.Type == engineType
                    )
                    .Select(kvp => kvp.Value)
                    .UnionBy(await _clearMLService.GetTasksForQueueAsync(queueName, cancellationToken), t => t.Id)
                    .ToDictionary(t => t.Id);

                queuePositionsPerEngineType[engineType] = tasksPerEngineType
                    .Values.Where(t => t.Status is ClearMLTaskStatus.Queued or ClearMLTaskStatus.Created)
                    .OrderBy(t => t.Created)
                    .Select((t, i) => (Position: i, Task: t))
                    .GroupBy(e => e.Task.Name)
                    .ToDictionary(e => e.Key, e => e.First().Position);

                _queueSizePerEngineType[engineType] = queuePositionsPerEngineType[engineType].Count;
            }

            var dataAccessContext = scope.ServiceProvider.GetRequiredService<IDataAccessContext>();
            foreach (ITrainingEngine engine in engineToBuildServiceDict.Keys)
            {
                IPlatformService platformService = scope.ServiceProvider.GetRequiredKeyedService<IPlatformService>(
                    engine.Type switch
                    {
                        EngineType.SmtTransfer => EngineGroup.Translation,
                        EngineType.Nmt => EngineGroup.Translation,
                        EngineType.Statistical => EngineGroup.WordAlignment,
                        _
                            => throw new InvalidEnumArgumentException(
                                nameof(engine.Type),
                                (int)engine.Type,
                                typeof(EngineType)
                            )
                    }
                );
                if (engine.CurrentBuild is null || !tasks.TryGetValue(engine.CurrentBuild.JobId, out ClearMLTask? task))
                    continue;

                if (
                    engine.CurrentBuild.JobState is BuildJobState.Pending
                    && task.Status is ClearMLTaskStatus.Queued or ClearMLTaskStatus.Created
                )
                {
                    await UpdateTrainJobStatus(
                        platformService,
                        engine.CurrentBuild.BuildId,
                        new ProgressStatus(step: 0, percentCompleted: 0.0),
                        //CurrentBuild.BuildId should always equal the corresponding task.Name
                        queuePositionsPerEngineType[engine.Type][engine.CurrentBuild.BuildId] + 1,
                        GetPhases(task),
                        task.Started,
                        task.Completed,
                        cancellationToken
                    );
                }

                if (engine.CurrentBuild.Stage == BuildStage.Train)
                {
                    if (
                        engine.CurrentBuild.JobState is BuildJobState.Pending
                        && task.Status
                            is ClearMLTaskStatus.InProgress
                                or ClearMLTaskStatus.Stopped
                                or ClearMLTaskStatus.Failed
                                or ClearMLTaskStatus.Completed
                    )
                    {
                        bool canceled = !await TrainJobStartedAsync(
                            dataAccessContext,
                            engineToBuildServiceDict[engine],
                            platformService,
                            engine.EngineId,
                            engine.CurrentBuild.BuildId,
                            cancellationToken
                        );
                        if (canceled)
                            continue;
                    }

                    switch (task.Status)
                    {
                        case ClearMLTaskStatus.InProgress:
                        {
                            double? progress = null;
                            if (task.Runtime.TryGetValue("progress", out string? progressStr))
                                progress = int.Parse(progressStr, CultureInfo.InvariantCulture) / 100.0;
                            string? message = GetUserProperty(task, "message");
                            await UpdateTrainJobStatus(
                                platformService,
                                engine.CurrentBuild.BuildId,
                                new ProgressStatus(task.LastIteration ?? 0, progress, message),
                                queueDepth: 0,
                                GetPhases(task),
                                task.Started,
                                task.Completed,
                                cancellationToken
                            );
                            break;
                        }

                        case ClearMLTaskStatus.Completed:
                        {
                            string? message = GetUserProperty(task, "message");
                            await UpdateTrainJobStatus(
                                platformService,
                                engine.CurrentBuild.BuildId,
                                new ProgressStatus(task.LastIteration ?? 0, percentCompleted: 1.0, message),
                                queueDepth: 0,
                                GetPhases(task),
                                task.Started,
                                task.Completed,
                                cancellationToken
                            );
                            bool canceling = !await TrainJobCompletedAsync(
                                engineToBuildServiceDict[engine],
                                engine.Type,
                                engine.EngineId,
                                engine.CurrentBuild.BuildId,
                                (int)GetMetric(task, SummaryMetric, TrainCorpusSizeVariant),
                                GetMetric(task, SummaryMetric, ConfidenceVariant),
                                engine.CurrentBuild.Options,
                                cancellationToken
                            );
                            if (canceling)
                            {
                                await TrainJobCanceledAsync(
                                    dataAccessContext,
                                    engineToBuildServiceDict[engine],
                                    platformService,
                                    engine.EngineId,
                                    engine.CurrentBuild.BuildId,
                                    cancellationToken
                                );
                            }
                            break;
                        }

                        case ClearMLTaskStatus.Stopped:
                        {
                            await TrainJobCanceledAsync(
                                dataAccessContext,
                                engineToBuildServiceDict[engine],
                                platformService,
                                engine.EngineId,
                                engine.CurrentBuild.BuildId,
                                cancellationToken
                            );
                            break;
                        }

                        case ClearMLTaskStatus.Failed:
                        {
                            await TrainJobFaultedAsync(
                                dataAccessContext,
                                engineToBuildServiceDict[engine],
                                platformService,
                                engine.EngineId,
                                engine.CurrentBuild.BuildId,
                                $"{task.StatusReason} : {task.StatusMessage}",
                                cancellationToken
                            );
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while monitoring ClearML tasks.");
        }
    }

    private async Task<bool> TrainJobStartedAsync(
        IDataAccessContext dataAccessContext,
        IBuildJobService buildJobService,
        IPlatformService platformService,
        string engineId,
        string buildId,
        CancellationToken cancellationToken = default
    )
    {
        bool success = await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                if (!await buildJobService.BuildJobStartedAsync(engineId, buildId, ct))
                    return false;
                await platformService.BuildStartedAsync(buildId, CancellationToken.None);
                return true;
            },
            cancellationToken: cancellationToken
        );
        await UpdateTrainJobStatus(
            platformService,
            buildId,
            new ProgressStatus(0),
            0,
            null,
            cancellationToken: cancellationToken
        );
        _logger.LogInformation("Build started ({BuildId})", buildId);
        return success;
    }

    private async Task<bool> TrainJobCompletedAsync(
        IBuildJobService buildJobService,
        EngineType engineType,
        string engineId,
        string buildId,
        int corpusSize,
        double confidence,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await buildJobService.StartBuildJobAsync(
                BuildJobRunnerType.Hangfire,
                engineType,
                engineId,
                buildId,
                BuildStage.Postprocess,
                (corpusSize, confidence),
                buildOptions,
                cancellationToken
            );
        }
        finally
        {
            _curBuildStatus.Remove(buildId);
        }
    }

    private async Task TrainJobFaultedAsync(
        IDataAccessContext dataAccessContext,
        IBuildJobService buildJobService,
        IPlatformService platformService,
        string engineId,
        string buildId,
        string message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await dataAccessContext.WithTransactionAsync(
                async (ct) =>
                {
                    await platformService.BuildFaultedAsync(buildId, message, ct);
                    await buildJobService.BuildJobFinishedAsync(
                        engineId,
                        buildId,
                        buildComplete: false,
                        CancellationToken.None
                    );
                },
                cancellationToken: cancellationToken
            );
            _logger.LogError("Build faulted ({BuildId}). Error: {ErrorMessage}", buildId, message);
        }
        finally
        {
            _curBuildStatus.Remove(buildId);
        }
    }

    private async Task TrainJobCanceledAsync(
        IDataAccessContext dataAccessContext,
        IBuildJobService buildJobService,
        IPlatformService platformService,
        string engineId,
        string buildId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await dataAccessContext.WithTransactionAsync(
                async (ct) =>
                {
                    await platformService.BuildCanceledAsync(buildId, ct);
                    await buildJobService.BuildJobFinishedAsync(
                        engineId,
                        buildId,
                        buildComplete: false,
                        CancellationToken.None
                    );
                },
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Build canceled ({BuildId})", buildId);
        }
        finally
        {
            try
            {
                await _sharedFileService.DeleteAsync($"builds/{buildId}/", CancellationToken.None);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to to delete job data for build {BuildId}.", buildId);
            }
            _curBuildStatus.Remove(buildId);
        }
    }

    private async Task UpdateTrainJobStatus(
        IPlatformService platformService,
        string buildId,
        ProgressStatus progressStatus,
        int? queueDepth = null,
        IReadOnlyCollection<BuildPhase>? phases = null,
        DateTime? started = null,
        DateTime? completed = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            _curBuildStatus.TryGetValue(buildId, out ProgressStatus curProgressStatus)
            && curProgressStatus.Equals(progressStatus)
        )
        {
            return;
        }
        await platformService.UpdateBuildStatusAsync(
            buildId,
            progressStatus,
            queueDepth,
            phases,
            started,
            completed,
            cancellationToken
        );
        _curBuildStatus[buildId] = progressStatus;
    }

    private static double GetMetric(ClearMLTask task, string metric, string variant)
    {
        if (!task.LastMetrics.TryGetValue(metric, out IReadOnlyDictionary<string, ClearMLMetricsEvent>? metricVariants))
            return 0;

        if (!metricVariants.TryGetValue(variant, out ClearMLMetricsEvent? metricEvent))
            return 0;

        return metricEvent.Value;
    }

    private static IReadOnlyCollection<BuildPhase> GetPhases(ClearMLTask task)
    {
        return
        [
            new BuildPhase
            {
                Stage = BuildPhaseStage.Inference,
                Step = GetUserPropertyInt(task, "inference_step"),
                StepCount = GetUserPropertyInt(task, "inference_step_count"),
                Started = GetUserPropertyDateTime(task, "inference_started"),
            },
            new BuildPhase
            {
                Stage = BuildPhaseStage.Train,
                Step = GetUserPropertyInt(task, "train_step"),
                StepCount = GetUserPropertyInt(task, "train_step_count"),
                Started = GetUserPropertyDateTime(task, "train_started"),
            }
        ];
    }

    private static string? GetUserProperty(ClearMLTask task, string name)
    {
        if (
            !task.Hyperparams.TryGetValue(
                UserProperties,
                out IReadOnlyDictionary<string, ClearMLParamsItem>? paramsItems
            )
        )
        {
            return null;
        }

        if (!paramsItems.TryGetValue(name, out ClearMLParamsItem? paramsItem))
            return null;

        return paramsItem.Value;
    }

    private static DateTime? GetUserPropertyDateTime(ClearMLTask task, string name)
    {
        return DateTime.TryParse(GetUserProperty(task, name), out DateTime value)
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : null;
    }

    private static int? GetUserPropertyInt(ClearMLTask task, string name)
    {
        return int.TryParse(GetUserProperty(task, name), out int value) ? value : null;
    }

    private static string CreateMD5(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = MD5.HashData(inputBytes);

        return Convert.ToHexString(hashBytes).ToLower();
    }
}
