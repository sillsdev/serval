using Microsoft.Extensions.DependencyInjection;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ClearMLMonitorServiceTests
{
    // Test constants
    private const string DefaultEngineId = "engine-123";
    private const string DefaultBuildId = "build-456";
    private const string DefaultJobId = "job-789";
    private const string DefaultProjectId = "proj1";

    // Service under test
    private ClearMLMonitorService _service;

    // Dependencies
    private IClearMLService _clearMLService;
    private ISharedFileService _sharedFileService;
    private ILogger<ClearMLMonitorService> _logger;
    private IServiceProvider _serviceProvider;
    private IOptionsMonitor<BuildJobOptions> _buildJobOptions;
    private IOptionsMonitor<ClearMLOptions> _clearMLOptions;
    private IDataAccessContext _dataAccessContext;
    private IPlatformService _platformService;
    private IBuildJobService<TranslationEngine> _translationBuildJobService;
    private IBuildJobService<WordAlignmentEngine> _wordAlignmentBuildJobService;

    [SetUp]
    public void Setup()
    {
        // Create mocks for all dependencies
        SetupMocks();

        // Configure common mock behaviors
        ConfigureMockBehaviors();

        // Create service provider
        _serviceProvider = CreateServiceProvider();

        // Create service under test
        _service = new ClearMLMonitorService(
            _serviceProvider,
            _clearMLService,
            _sharedFileService,
            _clearMLOptions,
            _buildJobOptions,
            _logger
        );
    }

    private void SetupMocks()
    {
        _clearMLService = Substitute.For<IClearMLService>();
        _sharedFileService = Substitute.For<ISharedFileService>();
        _logger = Substitute.For<ILogger<ClearMLMonitorService>>();
        _dataAccessContext = Substitute.For<IDataAccessContext>();
        _platformService = Substitute.For<IPlatformService>();
        _translationBuildJobService = Substitute.For<IBuildJobService<TranslationEngine>>();
        _wordAlignmentBuildJobService = Substitute.For<IBuildJobService<WordAlignmentEngine>>();

        _buildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
        _clearMLOptions = Substitute.For<IOptionsMonitor<ClearMLOptions>>();
    }

    private void ConfigureMockBehaviors()
    {
        _buildJobOptions.CurrentValue.Returns(
            new BuildJobOptions { ClearML = [new ClearMLBuildQueue { EngineType = EngineType.Nmt, Queue = "default" }] }
        );

        _clearMLOptions.CurrentValue.Returns(
            new ClearMLOptions { BuildPollingTimeout = TimeSpan.FromSeconds(10), BuildPollingEnabled = true }
        );
    }

    private IServiceProvider CreateServiceProvider()
    {
        ServiceCollection serviceCollection = new ServiceCollection();

        // Register services
        serviceCollection.AddScoped(_ => _translationBuildJobService);
        serviceCollection.AddScoped(_ => _wordAlignmentBuildJobService);
        serviceCollection.AddScoped(_ => _dataAccessContext);
        serviceCollection.AddScoped(_ => _platformService);

        // Register keyed services
        serviceCollection.AddKeyedScoped<IPlatformService>(EngineGroup.Translation, (_, _) => _platformService);
        serviceCollection.AddKeyedScoped<IPlatformService>(EngineGroup.WordAlignment, (_, _) => _platformService);

        return serviceCollection.BuildServiceProvider();
    }

    // Helper method to create test engines
    private static TranslationEngine CreateTestEngine(
        string engineId = DefaultEngineId,
        string buildId = DefaultBuildId,
        string jobId = DefaultJobId,
        BuildJobState jobState = BuildJobState.Pending,
        BuildStage stage = BuildStage.Train
    )
    {
        return new TranslationEngine
        {
            EngineId = engineId,
            Type = EngineType.Nmt,
            CurrentBuild = new Build
            {
                BuildId = buildId,
                JobId = jobId,
                JobState = jobState,
                BuildJobRunner = BuildJobRunnerType.ClearML,
                Stage = stage
            },
            SourceLanguage = "en",
            TargetLanguage = "fr",
            IsModelPersisted = true,
        };
    }

    // Helper method to create ClearML tasks
    private static ClearMLTask CreateClearMLTask(
        string id,
        string name,
        ClearMLTaskStatus status,
        DateTime created,
        Dictionary<string, string>? runtime = null,
        Dictionary<string, IReadOnlyDictionary<string, ClearMLMetricsEvent>>? lastMetrics = null,
        int lastIteration = 0,
        string? statusMessage = null,
        string? statusReason = null
    )
    {
        return new ClearMLTask
        {
            Id = id,
            Name = name,
            Status = status,
            Created = created,
            Runtime = runtime ?? new Dictionary<string, string>(),
            LastMetrics = lastMetrics ?? new Dictionary<string, IReadOnlyDictionary<string, ClearMLMetricsEvent>>(),
            LastIteration = lastIteration,
            StatusMessage = statusMessage,
            StatusReason = statusReason,
            Project = new ClearMLProject { Id = DefaultProjectId }
        };
    }

    [Test]
    public async Task MonitorClearMLTasksPerDomain_QueuedStatus_UpdatesQueuePosition()
    {
        TranslationEngine engine = CreateTestEngine();
        SetupBuildingEngines(engine);

        ClearMLTask task = CreateClearMLTask(
            id: engine!.CurrentBuild!.JobId,
            name: engine.CurrentBuild.BuildId,
            status: ClearMLTaskStatus.Queued,
            created: DateTime.UtcNow
        );

        SetupClearMLTasks(task);

        using IServiceScope scope = _serviceProvider.CreateScope();
        await _service.MonitorClearMLTasksPerDomain(scope, CancellationToken.None);

        int queueSize = _service.GetQueueSize(EngineType.Nmt);
        Assert.That(queueSize, Is.EqualTo(1));

        await VerifyStatusUpdate(
            buildId: engine.CurrentBuild.BuildId,
            step: 0,
            percentCompleted: 0.0,
            queuePosition: null // null means we don't care about the value
        );
    }

    [Test]
    public async Task MonitorClearMLTasksPerDomain_InProgress_UpdatesProgressCorrectly()
    {
        TranslationEngine engine = CreateTestEngine(jobState: BuildJobState.Active);
        SetupBuildingEngines(engine);

        Dictionary<string, string> runtimeInfo = new Dictionary<string, string>
        {
            { "progress", "50" },
            { "message", "Training epoch 5/10" }
        };

        ClearMLTask task = CreateClearMLTask(
            id: engine!.CurrentBuild!.JobId,
            name: engine.CurrentBuild.BuildId,
            status: ClearMLTaskStatus.InProgress,
            created: DateTime.UtcNow,
            runtime: runtimeInfo,
            lastIteration: 5
        );

        SetupClearMLTasks(task);

        using IServiceScope scope = _serviceProvider.CreateScope();
        await _service.MonitorClearMLTasksPerDomain(scope, CancellationToken.None);

        // Assert
        await VerifyStatusUpdate(
            buildId: engine.CurrentBuild.BuildId,
            step: 5,
            percentCompleted: 0.5,
            queuePosition: 0,
            message: "Training epoch 5/10"
        );
    }

    [Test]
    public async Task MonitorClearMLTasksPerDomain_CompletedStatus_ProperlyHandlesCompletion()
    {
        const int ExpectedCorpusSize = 1000;
        const double ExpectedConfidence = 0.95;

        TranslationEngine engine = CreateTestEngine(jobState: BuildJobState.Active, stage: BuildStage.Train);
        SetupBuildingEngines(engine);

        Dictionary<string, IReadOnlyDictionary<string, ClearMLMetricsEvent>> lastMetrics = new Dictionary<
            string,
            IReadOnlyDictionary<string, ClearMLMetricsEvent>
        >
        {
            [ClearMLMonitorService.SummaryMetric] = new Dictionary<string, ClearMLMetricsEvent>
            {
                [ClearMLMonitorService.TrainCorpusSizeVariant] = new ClearMLMetricsEvent { Value = ExpectedCorpusSize },
                [ClearMLMonitorService.ConfidenceVariant] = new ClearMLMetricsEvent { Value = ExpectedConfidence }
            }
        };

        Dictionary<string, string> runtime = new Dictionary<string, string> { ["message"] = "Training complete" };

        ClearMLTask task = CreateClearMLTask(
            id: engine!.CurrentBuild!.JobId,
            name: engine.CurrentBuild.BuildId,
            status: ClearMLTaskStatus.Completed,
            created: DateTime.UtcNow,
            runtime: runtime,
            lastMetrics: lastMetrics,
            lastIteration: 100
        );

        SetupClearMLTasks(task);

        _translationBuildJobService
            .StartBuildJobAsync(
                BuildJobRunnerType.Hangfire,
                engine.Type,
                engine.EngineId,
                engine.CurrentBuild.BuildId,
                BuildStage.Postprocess,
                Arg.Is<(int, double)>(x => x.Item1 == ExpectedCorpusSize && x.Item2 == ExpectedConfidence),
                engine.CurrentBuild.Options,
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        using IServiceScope scope = _serviceProvider.CreateScope();
        await _service.MonitorClearMLTasksPerDomain(scope, CancellationToken.None);

        // Assert - Verify status update
        await VerifyStatusUpdate(
            buildId: engine.CurrentBuild.BuildId,
            step: 100,
            percentCompleted: 1.0,
            queuePosition: 0,
            message: "Training complete"
        );

        // Assert - Verify postprocessing job started
        await _translationBuildJobService
            .Received(1)
            .StartBuildJobAsync(
                BuildJobRunnerType.Hangfire,
                engine.Type,
                engine.EngineId,
                engine.CurrentBuild.BuildId,
                BuildStage.Postprocess,
                Arg.Is<(int, double)>(x => x.Item1 == ExpectedCorpusSize && x.Item2 == ExpectedConfidence),
                engine.CurrentBuild.Options,
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task MonitorClearMLTasksPerDomain_StoppedStatus_CancelsBuildAndCleansUp()
    {
        TranslationEngine engine = CreateTestEngine(jobState: BuildJobState.Active, stage: BuildStage.Train);

        // Create real data access context
        MemoryDataAccessContext dataAccessContext = new MemoryDataAccessContext();
        IServiceProvider serviceProvider = CreateServiceProviderWithRealDataContext(dataAccessContext);

        // Create service with real data context
        _service = new ClearMLMonitorService(
            serviceProvider,
            _clearMLService,
            _sharedFileService,
            _clearMLOptions,
            _buildJobOptions,
            _logger
        );

        // Setup building engines
        SetupBuildingEngines(engine);

        // Setup stopped task
        ClearMLTask task = CreateClearMLTask(
            id: engine!.CurrentBuild!.JobId,
            name: engine.CurrentBuild.BuildId,
            status: ClearMLTaskStatus.Stopped,
            created: DateTime.UtcNow,
            statusMessage: "Manually stopped",
            statusReason: "User requested stop"
        );

        SetupClearMLTasks(task);

        using IServiceScope scope = serviceProvider.CreateScope();
        await _service.MonitorClearMLTasksPerDomain(scope, CancellationToken.None);

        // Assert
        await VerifyCancellationFlow(engine);
    }

    [Test]
    public async Task MonitorClearMLTasksPerDomain_FailedStatus_ProperlyHandlesFailure()
    {
        const string ErrorMessage = "Training failed due to GPU memory error";
        const string ErrorReason = "RuntimeError";

        TranslationEngine engine = CreateTestEngine(jobState: BuildJobState.Active, stage: BuildStage.Train);

        // Use real implementations
        MemoryDataAccessContext dataAccessContext = new MemoryDataAccessContext();
        _sharedFileService = new SharedFileService(Substitute.For<ILoggerFactory>());

        // Create dummy files
        await _sharedFileService.OpenWriteAsync($"builds/{engine!.CurrentBuild!.BuildId}/model.bin");
        await _sharedFileService.OpenWriteAsync($"builds/{engine.CurrentBuild.BuildId}/logs.txt");

        // Setup service with real implementations
        IServiceProvider serviceProvider = CreateServiceProviderWithRealDataContext(dataAccessContext);
        _service = new ClearMLMonitorService(
            serviceProvider,
            _clearMLService,
            _sharedFileService,
            _clearMLOptions,
            _buildJobOptions,
            _logger
        );

        // Setup building engines
        SetupBuildingEngines(engine);

        // Setup failed task
        ClearMLTask task = CreateClearMLTask(
            id: engine.CurrentBuild.JobId,
            name: engine.CurrentBuild.BuildId,
            status: ClearMLTaskStatus.Failed,
            created: DateTime.UtcNow,
            statusMessage: ErrorMessage,
            statusReason: ErrorReason
        );

        SetupClearMLTasks(task);

        using IServiceScope scope = serviceProvider.CreateScope();
        await _service.MonitorClearMLTasksPerDomain(scope, CancellationToken.None);

        // Assert
        await VerifyFailureHandling(engine, ErrorReason, ErrorMessage);

        // Verify files were actually deleted
        var files = await _sharedFileService.ListFilesAsync($"builds/{engine.CurrentBuild.BuildId}/");
        Assert.That(files, Is.Empty, "Build files should have been deleted");
    }

    #region Helper Methods

    private void SetupBuildingEngines(TranslationEngine engine)
    {
        _translationBuildJobService
            .GetBuildingEnginesAsync(BuildJobRunnerType.ClearML, Arg.Any<CancellationToken>())
            .Returns(new[] { engine });
    }

    private void SetupClearMLTasks(ClearMLTask task)
    {
        _clearMLService
            .GetTasksByIdAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { task });
    }

    private IServiceProvider CreateServiceProviderWithRealDataContext(IDataAccessContext dataAccessContext)
    {
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<IDataAccessContext>(_ => dataAccessContext);
        serviceCollection.AddScoped(_ => _translationBuildJobService);
        serviceCollection.AddScoped(_ => _wordAlignmentBuildJobService);
        serviceCollection.AddScoped(_ => _platformService);
        serviceCollection.AddKeyedScoped<IPlatformService>(EngineGroup.Translation, (_, _) => _platformService);
        serviceCollection.AddKeyedScoped<IPlatformService>(EngineGroup.WordAlignment, (_, _) => _platformService);

        return serviceCollection.BuildServiceProvider();
    }

    private async Task VerifyStatusUpdate(
        string buildId,
        int step,
        double percentCompleted,
        int? queuePosition = null,
        string? message = null
    )
    {
        if (queuePosition.HasValue)
        {
            await _platformService
                .Received(1)
                .UpdateBuildStatusAsync(
                    buildId,
                    Arg.Is<ProgressStatus>(s =>
                        s.Step == step
                        && s.PercentCompleted == percentCompleted
                        && (message == null || s.Message == message)
                    ),
                    queuePosition.Value,
                    Arg.Any<CancellationToken>()
                );
        }
        else
        {
            await _platformService
                .Received(1)
                .UpdateBuildStatusAsync(
                    buildId,
                    Arg.Is<ProgressStatus>(s =>
                        s.Step == step
                        && s.PercentCompleted == percentCompleted
                        && (message == null || s.Message == message)
                    ),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    private async Task VerifyCancellationFlow(TranslationEngine engine)
    {
        await _platformService
            .Received(1)
            .BuildCanceledAsync(engine!.CurrentBuild!.BuildId, Arg.Any<CancellationToken>());

        await _translationBuildJobService
            .Received(1)
            .BuildJobFinishedAsync(engine.EngineId, engine.CurrentBuild.BuildId, false, Arg.Any<CancellationToken>());

        await _sharedFileService
            .Received(1)
            .DeleteAsync($"builds/{engine.CurrentBuild.BuildId}/", Arg.Any<CancellationToken>());
    }

    private async Task VerifyFailureHandling(TranslationEngine engine, string errorReason, string errorMessage)
    {
        await _platformService
            .Received(1)
            .BuildFaultedAsync(
                engine!.CurrentBuild!.BuildId,
                Arg.Is<string>(msg => msg.Contains(errorReason) && msg.Contains(errorMessage)),
                Arg.Any<CancellationToken>()
            );

        await _translationBuildJobService
            .Received(1)
            .BuildJobFinishedAsync(engine.EngineId, engine.CurrentBuild.BuildId, false, Arg.Any<CancellationToken>());
    }

    #endregion
}
