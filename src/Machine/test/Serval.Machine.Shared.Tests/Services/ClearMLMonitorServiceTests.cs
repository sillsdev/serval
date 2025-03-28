using Microsoft.Extensions.DependencyInjection;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ClearMLMonitorServiceTests
{
    private TestableClearMLMonitorService _service;
    private IClearMLService _clearMLService;
    private ISharedFileService _sharedFileService;
    private ILogger<ClearMLMonitorService> _logger;
    private IServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        _clearMLService = Substitute.For<IClearMLService>();
        _sharedFileService = Substitute.For<ISharedFileService>();
        _logger = Substitute.For<ILogger<ClearMLMonitorService>>();

        var serviceCollection = new ServiceCollection();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        var clearMLOptions = Substitute.For<IOptionsMonitor<ClearMLOptions>>();
        clearMLOptions.CurrentValue.Returns(
            new ClearMLOptions { BuildPollingTimeout = TimeSpan.FromSeconds(5), BuildPollingEnabled = true }
        );

        var buildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
        buildJobOptions.CurrentValue.Returns(
            new BuildJobOptions
            {
                ClearML = new List<ClearMLBuildQueue>
                {
                    new ClearMLBuildQueue { EngineType = EngineType.Nmt, Queue = "nmt_queue" }
                },
                PreserveBuildFiles = false
            }
        );

        _service = new TestableClearMLMonitorService(
            _serviceProvider,
            _clearMLService,
            _sharedFileService,
            clearMLOptions,
            buildJobOptions,
            _logger
        );
    }

    [Test]
    public async Task GetQueueSize_ReturnsCorrectValue()
    {
        // Arrange
        var engineType = EngineType.Nmt;

        // Setup mock response
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
            .GetTasksForQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ClearMLTask>>(fakeTasks));

        // Act
        await _service.TestDoWorkAsync(_serviceProvider.CreateScope(), CancellationToken.None);
        int queueSize = _service.GetQueueSize(engineType);

        // Assert
        Assert.That(queueSize, Is.GreaterThan(0));
    }

    // [Test]
    // public async Task MonitorClearMLTasksPerDomain_HandlesExceptions()
    // {
    //     // Arrange
    //     _clearMLService
    //         .GetTasksForQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    //         .Throws(new InvalidOperationException("ClearML encountered an error while retrieving tasks."));
    //     var scope = _serviceProvider.CreateScope();

    //     // Act & Assert
    //     await _service.TestDoWorkAsync(scope, CancellationToken.None);

    //     // Verify error was logged
    //     _logger
    //         .Received(1)
    //         .Log(
    //             LogLevel.Error,
    //             Arg.Any<EventId>(),
    //             Arg.Any<object>(),
    //             Arg.Any<Exception>(),
    //             Arg.Any<Func<object, Exception, string>>()
    //         );
    // }
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
    // Expose protected method for testing
    public Task TestDoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        return DoWorkAsync(scope, cancellationToken);
    }
}
