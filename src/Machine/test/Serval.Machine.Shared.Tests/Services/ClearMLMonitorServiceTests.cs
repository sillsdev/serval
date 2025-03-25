using Microsoft.Extensions.DependencyInjection;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ClearMLMonitorServiceTests
{
    private ClearMLMonitorService _service;
    private FakeClearMLService _clearMLService;
    private FakeSharedFileService _sharedFileService;
    private FakeLogger<ClearMLMonitorService> _logger;
    private IServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        _clearMLService = new FakeClearMLService();
        _sharedFileService = new FakeSharedFileService();
        _logger = new FakeLogger<ClearMLMonitorService>();

        var serviceCollection = new ServiceCollection();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        var clearMLOptions = Options.Create(
            new ClearMLOptions { BuildPollingTimeout = TimeSpan.FromSeconds(5), BuildPollingEnabled = true }
        );
        var buildJobOptions = Options.Create(new BuildJobOptions());

        _service = new ClearMLMonitorService(
            _serviceProvider,
            _clearMLService,
            _sharedFileService,
            clearMLOptions,
            buildJobOptions,
            _logger
        );
    }

    [Test]
    public void GetQueueSize_ReturnsCorrectValue()
    {
        // Arrange
        var engineType = EngineType.Nmt;

        // Act
        int queueSize = _service.GetQueueSize(engineType);

        // Assert
        Assert.AreEqual(0, queueSize);
    }

    [Test]
    public async Task MonitorClearMLTasksPerDomain_HandlesExceptions()
    {
        // Arrange: Simulate exception in ClearML service
        _clearMLService.ThrowException = true;
        var scope = _serviceProvider.CreateScope();

        // Act & Assert: Should not throw, only log error
        Assert.DoesNotThrowAsync(() => _service.DoWorkAsync(scope, CancellationToken.None));
        Assert.IsTrue(_logger.HasError);
    }

    // More tests can be added here for job state updates
}

// Fake implementations for dependencies

public class FakeClearMLService : IClearMLService
{
    public bool ThrowException { get; set; } = false;

    public Task<Dictionary<string, ClearMLTask>> GetTasksByIdAsync(
        IEnumerable<string> taskIds,
        CancellationToken cancellationToken
    )
    {
        if (ThrowException)
            throw new Exception("ClearML error");
        return Task.FromResult(new Dictionary<string, ClearMLTask>());
    }

    public Task<IEnumerable<ClearMLTask>> GetTasksForQueueAsync(string queue, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ClearMLTask>>(new List<ClearMLTask>());
    }
}

public class FakeSharedFileService : ISharedFileService
{
    public Task DeleteAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class FakeLogger<T> : ILogger<T>
{
    public bool HasError { get; private set; }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter
    )
    {
        if (logLevel == LogLevel.Error)
            HasError = true;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) => null;
}
