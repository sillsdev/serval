using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ClearMLMonitorServiceTests
{
    private ClearMLMonitorService _service;
    private IClearMLService _clearMLService;
    private ISharedFileService _sharedFileService;
    private ILogger<ClearMLMonitorService> _logger;
    private IServiceProvider _serviceProvider;
    private IOptionsMonitor<BuildJobOptions> _buildJobOptionsMock;
    private IOptionsMonitor<ClearMLOptions> _clearMLOptions;
    private IDataAccessContext _dataAccessContext;
    private IPlatformService _platformService;
    private Dictionary<string, ProgressStatus>? _curBuildStatus;

    private IBuildJobService<TranslationEngine> _buildJobServiceTranslation;
    private IBuildJobService<WordAlignmentEngine> _buildJobServiceWordAlignment;

    [SetUp]
    public void SetUp()
    {
        var serviceCollection = new ServiceCollection();

        // Mock services
        _clearMLService = Substitute.For<IClearMLService>();
        _sharedFileService = Substitute.For<ISharedFileService>();
        _logger = Substitute.For<ILogger<ClearMLMonitorService>>();
        _dataAccessContext = Substitute.For<IDataAccessContext>();
        _platformService = Substitute.For<IPlatformService>();
        _curBuildStatus = Substitute.For<Dictionary<string, ProgressStatus>>();

        // Mock IBuildJobService for both TranslationEngine and WordAlignmentEngine
        _buildJobServiceTranslation = Substitute.For<IBuildJobService<TranslationEngine>>();
        _buildJobServiceWordAlignment = Substitute.For<IBuildJobService<WordAlignmentEngine>>();

        // Mock BuildJobOptions
        _buildJobOptionsMock = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
        _buildJobOptionsMock.CurrentValue.Returns(
            new BuildJobOptions
            {
                ClearML =
                [
                    new ClearMLBuildQueue
                    {
                        EngineType = EngineType.Nmt,
                        ModelType = "thot",
                        DockerImage = "default",
                        Queue = "default"
                    }
                ]
            }
        );

        // Mock ClearMLOptions
        _clearMLOptions = Substitute.For<IOptionsMonitor<ClearMLOptions>>();
        _clearMLOptions.CurrentValue.Returns(
            new ClearMLOptions { BuildPollingTimeout = TimeSpan.FromSeconds(10), BuildPollingEnabled = false }
        );

        // Register dependencies in DI container
        serviceCollection.AddScoped(_ => _clearMLService);
        serviceCollection.AddScoped(_ => _sharedFileService);
        serviceCollection.AddScoped(_ => _clearMLOptions);
        serviceCollection.AddScoped(_ => _buildJobOptionsMock);
        serviceCollection.AddScoped(_ => _logger);
        serviceCollection.AddScoped(_ => _dataAccessContext);
        serviceCollection.AddScoped(_ => _platformService);

        // Register both BuildJobServices
        serviceCollection.AddScoped(_ => _buildJobServiceTranslation);
        serviceCollection.AddScoped(_ => _buildJobServiceWordAlignment);

        _serviceProvider = serviceCollection.BuildServiceProvider(); // ✅ Build AFTER adding services

        // Create instance of ClearMLMonitorService
        _service = new ClearMLMonitorService(
            _serviceProvider,
            _clearMLService,
            _sharedFileService,
            _clearMLOptions,
            _buildJobOptionsMock,
            _logger
        );

        Console.WriteLine($"BuildJobOptions.ClearML Count: {_buildJobOptionsMock.CurrentValue.ClearML?.Count}");
    }

    [Test]
    public async Task GetQueueSize_ReturnsCorrectValue()
    {
        string jobId = "task1";
        string buildId = "build1";
        string engineId = "engine1";
        string queueName = "default";

        var build = new Build
        {
            BuildId = buildId,
            JobId = jobId,
            JobState = BuildJobState.Pending,
            BuildJobRunner = BuildJobRunnerType.ClearML,
            Stage = BuildStage.Train
        };

        var engine = new TranslationEngine
        {
            EngineId = engineId,
            Type = EngineType.Nmt,
            SourceLanguage = "en",
            TargetLanguage = "fr",
            IsModelPersisted = true,
            BuildRevision = 1,
            CurrentBuild = build
        };

        _buildJobServiceTranslation
            .GetBuildingEnginesAsync(BuildJobRunnerType.ClearML, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TranslationEngine>>(new[] { engine }));

        _buildJobServiceWordAlignment
            .GetBuildingEnginesAsync(BuildJobRunnerType.ClearML, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<WordAlignmentEngine>>(Array.Empty<WordAlignmentEngine>()));

        var clearMLTask = new ClearMLTask
        {
            Id = jobId,
            Name = buildId,
            Created = DateTime.UtcNow,
            Status = ClearMLTaskStatus.Queued,
            Runtime = new Dictionary<string, string>(),
            LastMetrics = new Dictionary<string, IReadOnlyDictionary<string, ClearMLMetricsEvent>>(),
            StatusMessage = "",
            StatusReason = "",
            Project = new ClearMLProject { Id = "proj1" }
        };

        _clearMLService
            .GetTasksByIdAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ClearMLTask>>(new[] { clearMLTask }));

        _clearMLService
            .GetTasksForQueueAsync(queueName, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ClearMLTask>>(Array.Empty<ClearMLTask>()));

        _platformService
            .UpdateBuildStatusAsync(
                Arg.Any<string>(),
                Arg.Any<ProgressStatus>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.CompletedTask);

        using var scope = _serviceProvider.CreateScope();
        await _service.MonitorClearMLTasksPerDomain(scope, CancellationToken.None);

        var queueSize = _service.GetQueueSize(EngineType.Nmt);
        Assert.That(queueSize, Is.EqualTo(1));

        await _clearMLService
            .Received(1)
            .GetTasksByIdAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TrainJobStartedAsync_Successful_ReturnsTrue()
    {
        string engineId = "engine123";
        string buildId = "build456";

        var buildJobService = Substitute.For<IBuildJobService>();
        buildJobService.BuildJobStartedAsync(engineId, buildId, Arg.Any<CancellationToken>()).Returns(true);

        _dataAccessContext
            .WithTransactionAsync(Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var func = callInfo.Arg<Func<CancellationToken, Task<bool>>>();
                var token = callInfo.ArgAt<CancellationToken>(1);
                return func(token); // this executes the inner delegate
            });

        bool result = await _service.TrainJobStartedAsync(
            _dataAccessContext,
            buildJobService,
            _platformService,
            engineId,
            buildId
        );

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task TrainJobCompletedAsync_Successful_ReturnsTrue()
    {
        string engineId = "engine123";
        string buildId = "build456";
        int corpusSize = 1000;
        double confidence = 0.95;
        string? buildOptions = "option1";

        var buildJobService = Substitute.For<IBuildJobService>();

        buildJobService
            .StartBuildJobAsync(
                Arg.Any<BuildJobRunnerType>(),
                Arg.Any<EngineType>(),
                engineId,
                buildId,
                Arg.Any<BuildStage>(),
                Arg.Any<object?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        // Mock the WithTransactionAsync method to just execute the transaction delegate
        _dataAccessContext
            .WithTransactionAsync(Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var func = callInfo.Arg<Func<CancellationToken, Task<bool>>>();
                var token = callInfo.ArgAt<CancellationToken>(1);
                return func(token); // execute the transaction logic
            });

        bool result = await _service.TrainJobCompletedAsync(
            buildJobService,
            EngineType.Nmt,
            engineId,
            buildId,
            corpusSize,
            confidence,
            buildOptions,
            CancellationToken.None
        );

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task TrainJobFaultedAsync_HandlesFailureAndLogsError()
    {
        // Arrange
        string engineId = "engine-123";
        string buildId = "build-456";
        string errorMessage = "Training failed due to GPU error";

        var buildJobService = Substitute.For<IBuildJobService>();
        var logMessages = new List<string>();

        // Setup logging capture
        _logger
            .When(x =>
                x.Log(
                    LogLevel.Error,
                    Arg.Any<EventId>(),
                    Arg.Any<object>(),
                    Arg.Any<Exception>(),
                    Arg.Any<Func<object, Exception?, string>>()
                )
            )
            .Do(callInfo =>
            {
                var state = callInfo.Arg<object>();
                var exception = callInfo.Arg<Exception>();
                var formatter = callInfo.Arg<Func<object, Exception, string>>();
                var message = formatter(state, exception);
                logMessages.Add(message);
                TestContext.WriteLine($"Logged: {message}");
            });

        _dataAccessContext
            .WithTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));

        await _service.TrainJobFaultedAsync(
            _dataAccessContext,
            buildJobService,
            _platformService,
            engineId,
            buildId,
            errorMessage,
            CancellationToken.None
        );

        // Assert that the necessary functions are being called
        await _platformService.Received(1).BuildFaultedAsync(buildId, errorMessage, Arg.Any<CancellationToken>());
        await buildJobService
            .Received(1)
            .BuildJobFinishedAsync(engineId, buildId, buildComplete: false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TrainJobCanceledAsync_HandlesCancellationAndCleansUp()
    {
        string engineId = "engine-123";
        string buildId = "build-456";

        var buildJobService = Substitute.For<IBuildJobService>();

        _dataAccessContext
            .WithTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var func = callInfo.Arg<Func<CancellationToken, Task>>();
                return func(CancellationToken.None);
            });

        _sharedFileService.DeleteAsync($"builds/{buildId}/", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.TrainJobCanceledAsync(
            _dataAccessContext,
            buildJobService,
            _platformService,
            engineId,
            buildId,
            CancellationToken.None
        );

        // Assert that the necessary methods are called
        await _platformService.Received(1).BuildCanceledAsync(buildId, Arg.Any<CancellationToken>());

        await buildJobService
            .Received(1)
            .BuildJobFinishedAsync(engineId, buildId, buildComplete: false, Arg.Any<CancellationToken>());

        await _sharedFileService.Received(1).DeleteAsync($"builds/{buildId}/", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateTrainJobStatus_WithExistingStatus_UpdatesCorrectly()
    {
        var platformService = Substitute.For<IPlatformService>();
        string buildId = "build-123";
        var initialStatus = new ProgressStatus(5, 0.25, "Initial status");

        // Mock the private _curBuildStatus dictionary
        var curBuildStatus = new Dictionary<string, ProgressStatus> { [buildId] = initialStatus };
        SetPrivateField(_service, "_curBuildStatus", curBuildStatus);

        var newStatus = new ProgressStatus(10, 0.5, "Updated status");

        // Act
        await _service.UpdateTrainJobStatus(
            platformService,
            buildId,
            newStatus,
            queueDepth: null,
            CancellationToken.None
        );

        // Assert
        await platformService
            .Received(1)
            .UpdateBuildStatusAsync(buildId, newStatus, null, Arg.Any<CancellationToken>());

        // Verify the dictionary was updated
        Assert.That(curBuildStatus[buildId], Is.EqualTo(newStatus));
    }

    private static void SetPrivateField<T>(T target, string fieldName, object value)
    {
        var field = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new InvalidOperationException($"Field {fieldName} not found");
        }
        field.SetValue(target, value);
    }
}
