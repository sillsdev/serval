using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Serval.Machine.Shared.Configuration;
using Serval.Machine.Shared.Models;
using Serval.Machine.Translation.Models;
using Serval.Machine.Translation.Services;
using Serval.Machine.WordAlignment.Models;
using Serval.Machine.WordAlignment.Services;
using SIL.Machine.Utils;

namespace Serval.Machine.Shared.Services;

[TestFixture(typeof(TranslationEngine), typeof(TranslationEngineClearMLMonitorService), EngineType.Nmt)]
[TestFixture(typeof(TranslationEngine), typeof(TranslationEngineClearMLMonitorService), EngineType.SmtTransfer)]
[TestFixture(typeof(WordAlignmentEngine), typeof(WordAlignmentEngineClearMLMonitorService), EngineType.Statistical)]
public class ClearMLMonitorServiceTests<TEngine, TClearMLMonitorService>(EngineType engineType)
    where TEngine : ITrainingEngine, new()
    where TClearMLMonitorService : ClearMLMonitorService<TEngine>
{
    // Test constants
    private const string DefaultEngineId = "engine-123";
    private const string DefaultBuildId = "build-456";
    private const string DefaultJobId = "job-789";
    private const string DefaultProjectId = "proj1";

    // Service under test
    private TClearMLMonitorService _service;

    // Dependencies
    private IClearMLService _clearMLService;
    private ISharedFileService _sharedFileService;
    private ILogger<ClearMLMonitorService<TEngine>> _logger;
    private IServiceProvider _serviceProvider;
    private IOptionsMonitor<BuildJobOptions> _buildJobOptions;
    private IOptionsMonitor<ClearMLOptions> _clearMLOptions;
    private IDataAccessContext _dataAccessContext;
    private IPlatformService _platformService;
    private IBuildJobService<TEngine> _buildJobService;

    // Test values

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
        _service = CreateService();
    }

    [TearDown]
    public void TearDown()
    {
        _dataAccessContext.Dispose();
        _service.Dispose();
    }

    private void SetupMocks()
    {
        _clearMLService = Substitute.For<IClearMLService>();
        _sharedFileService = Substitute.For<ISharedFileService>();
        _logger = Substitute.For<ILogger<TClearMLMonitorService>>();
        _dataAccessContext = Substitute.For<IDataAccessContext>();
        _platformService = Substitute.For<IPlatformService>();
        _buildJobService = Substitute.For<IBuildJobService<TEngine>>();

        _buildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
        _clearMLOptions = Substitute.For<IOptionsMonitor<ClearMLOptions>>();
    }

    private void ConfigureMockBehaviors()
    {
        _buildJobOptions.CurrentValue.Returns(
            new BuildJobOptions { ClearML = [new ClearMLBuildQueue { EngineType = engineType, Queue = "default" }] }
        );

        _clearMLOptions.CurrentValue.Returns(
            new ClearMLOptions { BuildPollingTimeout = TimeSpan.FromSeconds(10), BuildPollingEnabled = true }
        );
    }

    private TClearMLMonitorService CreateService(IServiceProvider? serviceProvider = null) =>
        (TClearMLMonitorService)
            Activator.CreateInstance(
                typeof(TClearMLMonitorService),
                serviceProvider ?? _serviceProvider,
                _clearMLService,
                _sharedFileService,
                _clearMLOptions,
                _buildJobOptions,
                _logger
            )!;

    private IServiceProvider CreateServiceProvider()
    {
        ServiceCollection serviceCollection = new ServiceCollection();

        // Register services
        serviceCollection.AddScoped(_ => _buildJobService);
        serviceCollection.AddScoped(_ => _dataAccessContext);
        serviceCollection.AddScoped(_ => _platformService);

        // Register keyed services
        serviceCollection.AddKeyedScoped<IPlatformService>(engineType.ToEngineGroup(), (_, _) => _platformService);

        return serviceCollection.BuildServiceProvider();
    }

    // Helper method to create test engines
    private TEngine CreateTestEngine(
        string engineId = DefaultEngineId,
        string buildId = DefaultBuildId,
        string jobId = DefaultJobId,
        BuildJobState jobState = BuildJobState.Pending,
        BuildStage stage = BuildStage.Train
    )
    {
        return new TEngine
        {
            EngineId = engineId,
            Type = engineType,
            CurrentBuild = new Build
            {
                BuildId = buildId,
                JobId = jobId,
                JobState = jobState,
                BuildJobRunner = BuildJobRunnerType.ClearML,
                Stage = stage,
                ExecutionData = new BuildExecutionData(),
            },
            SourceLanguage = "en",
            TargetLanguage = "fr",
        };
    }

    // Helper method to create ClearML tasks
    private static ClearMLTask CreateClearMLTask(
        string id,
        string name,
        ClearMLTaskStatus status,
        DateTime created,
        Dictionary<string, string>? runtime = null,
        Dictionary<string, IReadOnlyDictionary<string, ClearMLParamsItem>>? hyperParams = null,
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
            Hyperparams = hyperParams ?? new Dictionary<string, IReadOnlyDictionary<string, ClearMLParamsItem>>(),
            LastMetrics = lastMetrics ?? new Dictionary<string, IReadOnlyDictionary<string, ClearMLMetricsEvent>>(),
            LastIteration = lastIteration,
            StatusMessage = statusMessage,
            StatusReason = statusReason,
            Project = new ClearMLProject { Id = DefaultProjectId },
        };
    }

    [Test]
    public async Task MonitorClearMLTasksPerDomain_QueuedStatus_UpdatesQueuePosition()
    {
        TEngine engine = CreateTestEngine();
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

        int queueSize = _service.GetQueueSize(engineType);
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
        TEngine engine = CreateTestEngine(jobState: BuildJobState.Active);
        SetupBuildingEngines(engine);

        var runtimeInfo = new Dictionary<string, string> { { "progress", "50" } };

        var hyperParams = new Dictionary<string, IReadOnlyDictionary<string, ClearMLParamsItem>>
        {
            [ClearMLMonitorService<TEngine>.UserProperties] = new Dictionary<string, ClearMLParamsItem>
            {
                ["message"] = new() { Name = "message", Value = "Training epoch 5/10" },
            },
        };

        ClearMLTask task = CreateClearMLTask(
            id: engine!.CurrentBuild!.JobId,
            name: engine.CurrentBuild.BuildId,
            status: ClearMLTaskStatus.InProgress,
            created: DateTime.UtcNow,
            runtime: runtimeInfo,
            hyperParams: hyperParams,
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

        TEngine engine = CreateTestEngine(jobState: BuildJobState.Active, stage: BuildStage.Train);
        SetupBuildingEngines(engine);

        Dictionary<string, IReadOnlyDictionary<string, ClearMLMetricsEvent>> lastMetrics = new Dictionary<
            string,
            IReadOnlyDictionary<string, ClearMLMetricsEvent>
        >
        {
            [ClearMLMonitorService<TEngine>.SummaryMetric] = new Dictionary<string, ClearMLMetricsEvent>
            {
                [ClearMLMonitorService<TEngine>.TrainCorpusSizeVariant] = new() { Value = ExpectedCorpusSize },
                [ClearMLMonitorService<TEngine>.ConfidenceVariant] = new() { Value = ExpectedConfidence },
            },
        };

        var hyperParams = new Dictionary<string, IReadOnlyDictionary<string, ClearMLParamsItem>>
        {
            [ClearMLMonitorService<TEngine>.UserProperties] = new Dictionary<string, ClearMLParamsItem>
            {
                ["message"] = new() { Name = "message", Value = "Training complete" },
            },
        };

        ClearMLTask task = CreateClearMLTask(
            id: engine!.CurrentBuild!.JobId,
            name: engine.CurrentBuild.BuildId,
            status: ClearMLTaskStatus.Completed,
            created: DateTime.UtcNow,
            hyperParams: hyperParams,
            lastMetrics: lastMetrics,
            lastIteration: 100
        );

        SetupClearMLTasks(task);

        _buildJobService
            .StartBuildJobAsync(
                BuildJobRunnerType.Local,
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
        await _buildJobService
            .Received(1)
            .StartBuildJobAsync(
                BuildJobRunnerType.Local,
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
        TEngine engine = CreateTestEngine(jobState: BuildJobState.Active, stage: BuildStage.Train);

        // Create real data access context
        MemoryDataAccessContext dataAccessContext = new MemoryDataAccessContext();
        IServiceProvider serviceProvider = CreateServiceProviderWithRealDataContext(dataAccessContext);

        // Create service with real data context
        _service = CreateService(serviceProvider);

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

        TEngine engine = CreateTestEngine(jobState: BuildJobState.Active, stage: BuildStage.Train);

        // Use real implementations
        MemoryDataAccessContext dataAccessContext = new MemoryDataAccessContext();
        _sharedFileService = new SharedFileService(Substitute.For<ILoggerFactory>());

        // Create dummy files
        await _sharedFileService.OpenWriteAsync($"builds/{engine!.CurrentBuild!.BuildId}/model.bin");
        await _sharedFileService.OpenWriteAsync($"builds/{engine.CurrentBuild.BuildId}/logs.txt");

        // Setup service with real implementations
        IServiceProvider serviceProvider = CreateServiceProviderWithRealDataContext(dataAccessContext);
        _service = CreateService(serviceProvider);

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

    private void SetupBuildingEngines(TEngine engine)
    {
        _buildJobService
            .GetBuildingEnginesAsync(BuildJobRunnerType.ClearML, Arg.Any<CancellationToken>())
            .Returns([engine]);
    }

    private void SetupClearMLTasks(ClearMLTask task)
    {
        _clearMLService.GetTasksByIdAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns([task]);
    }

    private IServiceProvider CreateServiceProviderWithRealDataContext(IDataAccessContext dataAccessContext)
    {
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<IDataAccessContext>(_ => dataAccessContext);
        serviceCollection.AddScoped(_ => _buildJobService);
        serviceCollection.AddScoped(_ => _platformService);
        serviceCollection.AddKeyedScoped<IPlatformService>(engineType.ToEngineGroup(), (_, _) => _platformService);

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
                    Arg.Any<IReadOnlyCollection<BuildPhase>>(),
                    Arg.Any<DateTime?>(),
                    Arg.Any<DateTime?>(),
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
                    Arg.Any<IReadOnlyCollection<BuildPhase>>(),
                    Arg.Any<DateTime?>(),
                    Arg.Any<DateTime?>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    private async Task VerifyCancellationFlow(TEngine engine)
    {
        await _platformService
            .Received(1)
            .BuildCanceledAsync(engine!.CurrentBuild!.BuildId, Arg.Any<CancellationToken>());

        await _buildJobService
            .Received(1)
            .BuildJobFinishedAsync(engine.EngineId, engine.CurrentBuild.BuildId, false, Arg.Any<CancellationToken>());

        await _sharedFileService
            .Received(1)
            .DeleteAsync($"builds/{engine.CurrentBuild.BuildId}/", Arg.Any<CancellationToken>());
    }

    private async Task VerifyFailureHandling(TEngine engine, string errorReason, string errorMessage)
    {
        await _platformService
            .Received(1)
            .BuildFaultedAsync(
                engine!.CurrentBuild!.BuildId,
                Arg.Is<string>(msg => msg.Contains(errorReason) && msg.Contains(errorMessage)),
                Arg.Any<CancellationToken>()
            );

        await _buildJobService
            .Received(1)
            .BuildJobFinishedAsync(engine.EngineId, engine.CurrentBuild.BuildId, false, Arg.Any<CancellationToken>());
    }

    #endregion
}
