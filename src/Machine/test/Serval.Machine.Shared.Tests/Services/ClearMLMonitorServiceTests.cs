using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Serval.Machine.Shared.Services;

namespace Serval.Machine.Tests.Services;

[TestFixture]
public class ClearMLMonitorServiceTests
{
    private TestableClearMLMonitorService _service;
    private IClearMLService _clearMLService;
    private ISharedFileService _sharedFileService;
    private ILogger<ClearMLMonitorService> _logger;
    private IServiceProvider _serviceProvider;
    private IOptionsMonitor<BuildJobOptions> _buildJobOptionsMock;
    private IBuildJobService _buildJobService;
    private IDataAccessContext _dataAccessContext;
    private IPlatformService _platformService;
    private IOptionsMonitor<ClearMLOptions> _clearMLOptions;

    [SetUp]
    public void SetUp()
    {
        _clearMLService = Substitute.For<IClearMLService>();
        _sharedFileService = Substitute.For<ISharedFileService>();
        _logger = Substitute.For<ILogger<ClearMLMonitorService>>();
        _buildJobService = Substitute.For<IBuildJobService>();
        _dataAccessContext = Substitute.For<IDataAccessContext>();
        _platformService = Substitute.For<IPlatformService>();

        var serviceCollection = new ServiceCollection();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        // ✅ Fix: Properly mock BuildJobOptions
        _buildJobOptionsMock = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
        _buildJobOptionsMock.CurrentValue.Returns(
            new BuildJobOptions
            {
                ClearML = new List<ClearMLBuildQueue>
                {
                    new ClearMLBuildQueue { EngineType = EngineType.Nmt, Queue = "nmt_queue" }
                },
                PreserveBuildFiles = false
            }
        );

        _clearMLOptions = Substitute.For<IOptionsMonitor<ClearMLOptions>>();
        _clearMLOptions.CurrentValue.Returns(
            new ClearMLOptions { BuildPollingTimeout = TimeSpan.FromSeconds(5), BuildPollingEnabled = true }
        );

        _service = new TestableClearMLMonitorService(
            _serviceProvider,
            _clearMLService,
            _sharedFileService,
            _clearMLOptions,
            _buildJobOptionsMock,
            _logger
        );
    }

    [Test]
    public async Task GetQueueSize_ReturnsCorrectValue()
    {
        // Arrange
        var engineType = EngineType.Nmt;
        string queueName = "nmt_queue"; // Ensure this matches the test setup

        var fakeTasks = new List<ClearMLTask>
        {
            new ClearMLTask
            {
                Id = "task1",
                Name = "Test Task",
                Project = new ClearMLProject { Id = "project1" },
                Status = ClearMLTaskStatus.Queued,
                Created = DateTime.UtcNow,
                LastMetrics = new Dictionary<string, IReadOnlyDictionary<string, ClearMLMetricsEvent>>(),
                Runtime = new Dictionary<string, string>()
            }
        };

        _clearMLService
            .GetTasksForQueueAsync(Arg.Is<string>(q => q == queueName), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ClearMLTask>>(fakeTasks));

        // Act: Ensure DoWorkAsync() is actually executed
        await _service.TestDoWorkAsync(_serviceProvider.CreateScope(), CancellationToken.None);
        await Task.Delay(100); // Give time for async processing

        // Debug: Check if `GetTasksForQueueAsync()` was actually called
        await _clearMLService.Received(1).GetTasksForQueueAsync(queueName, Arg.Any<CancellationToken>());

        int queueSize = _service.GetQueueSize(engineType);

        Assert.That(queueSize, Is.EqualTo(fakeTasks.Count));
    }

    [Test]
    public async Task TrainJobStartedAsync_Successful_ReturnsTrue()
    {
        var dataAccessContext = Substitute.For<IDataAccessContext>();
        var buildJobService = Substitute.For<IBuildJobService>();
        var platformService = Substitute.For<IPlatformService>();
        string engineId = "engine123";
        string buildId = "build456";

        bool result = await _service.TestTrainJobStartedAsync(
            dataAccessContext,
            buildJobService,
            platformService,
            engineId,
            buildId,
            CancellationToken.None
        );

        Assert.That(result, Is.True);
    }
}

public class TestableClearMLMonitorService(
    IServiceProvider serviceProvider,
    IClearMLService clearMLService,
    ISharedFileService sharedFileService,
    IOptionsMonitor<ClearMLOptions> clearMLOptions,
    IOptionsMonitor<BuildJobOptions> buildJobOptions,
    ILogger<ClearMLMonitorService> logger
) : ClearMLMonitorService(serviceProvider, clearMLService, sharedFileService, clearMLOptions, buildJobOptions, logger)
{
    public Task TestDoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        return DoWorkAsync(scope, cancellationToken);
    }

    public async Task<bool> TestTrainJobStartedAsync(
        IDataAccessContext dataAccessContext,
        IBuildJobService buildJobService,
        IPlatformService platformService,
        string engineId,
        string buildId,
        CancellationToken cancellationToken = default
    )
    {
        var method = typeof(ClearMLMonitorService).GetMethod(
            "TrainJobStartedAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (method == null)
        {
            throw new InvalidOperationException(
                "TrainJobStartedAsync method not found! Ensure it exists and is protected/private."
            );
        }

        if (
            method.Invoke(
                this,
                new object[]
                {
                    dataAccessContext,
                    buildJobService,
                    platformService,
                    engineId,
                    buildId,
                    cancellationToken
                }
            )
            is not Task<bool> task
        )
        {
            throw new InvalidOperationException("Failed to invoke TrainJobStartedAsync.");
        }

        return await task;
    }

    // public Task<bool> TestTrainJobCompletedAsync(
    //     IBuildJobService buildJobService,
    //     EngineType engineType,
    //     string engineId,
    //     string buildId,
    //     int corpusSize,
    //     double confidence,
    //     string? buildOptions,
    //     CancellationToken cancellationToken
    // )
    // {
    //     return TrainJobCompletedAsync(
    //         buildJobService,
    //         engineType,
    //         engineId,
    //         buildId,
    //         corpusSize,
    //         confidence,
    //         buildOptions,
    //         cancellationToken
    //     );
    // }

    // public Task TestTrainJobFaultedAsync(
    //     IDataAccessContext dataAccessContext,
    //     IBuildJobService buildJobService,
    //     IPlatformService platformService,
    //     string engineId,
    //     string buildId,
    //     string message,
    //     CancellationToken cancellationToken
    // )
    // {
    //     return TrainJobFaultedAsync(
    //         dataAccessContext,
    //         buildJobService,
    //         platformService,
    //         engineId,
    //         buildId,
    //         message,
    //         cancellationToken
    //     );
    // }

    // public Task TestTrainJobCanceledAsync(
    //     IDataAccessContext dataAccessContext,
    //     IBuildJobService buildJobService,
    //     IPlatformService platformService,
    //     string engineId,
    //     string buildId,
    //     CancellationToken cancellationToken
    // )
    // {
    //     return TrainJobCanceledAsync(
    //         dataAccessContext,
    //         buildJobService,
    //         platformService,
    //         engineId,
    //         buildId,
    //         cancellationToken
    //     );
    // }
}
