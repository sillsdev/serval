using Microsoft.Extensions.DependencyInjection;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ClearMLMonitorServiceTests
{
    private TestableClearMLMonitorService _service;
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

        var clearMLOptions = new FakeOptionsMonitor<ClearMLOptions>(
            new ClearMLOptions { BuildPollingTimeout = TimeSpan.FromSeconds(5), BuildPollingEnabled = true }
        );

        var buildJobOptions = new FakeOptionsMonitor<BuildJobOptions>(
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

        // Act
        await _service.TestDoWorkAsync(_serviceProvider.CreateScope(), CancellationToken.None); // Ensures queues are populated
        int queueSize = _service.GetQueueSize(engineType);

        // Assert
        Assert.That(queueSize, Is.GreaterThan(0)); // Now expecting at least one task in queue
    }

    [Test]
    public void MonitorClearMLTasksPerDomain_HandlesExceptions()
    {
        // Arrange: Simulate exception in ClearML service
        _clearMLService.ThrowException = true;
        var scope = _serviceProvider.CreateScope();

        // Act & Assert: Should not throw, only log error
        Assert.DoesNotThrowAsync(() => _service.TestDoWorkAsync(scope, CancellationToken.None));
        Assert.IsTrue(_logger.HasError);
    }
}

// ============================
// Fake implementation of IOptionsMonitor<T>
// ============================

public class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
{
    private readonly T _currentValue = currentValue ?? throw new ArgumentNullException(nameof(currentValue));

    public T CurrentValue => _currentValue;

    public T Get(string? name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _currentValue;
    }

    public IDisposable OnChange(Action<T, string> listener) => Disposable.Empty;
}

// ============================
// Fake implementations for dependencies
// ============================

public class FakeClearMLService : IClearMLService
{
    public bool ThrowException { get; set; } = false;

    public Task<string> CreateProjectAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Guid.NewGuid().ToString());

    public Task<bool> DeleteProjectAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<string?> GetProjectIdAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(Guid.NewGuid().ToString());

    public Task<string> CreateTaskAsync(
        string buildId,
        string projectId,
        string script,
        string dockerImage,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Guid.NewGuid().ToString());

    public Task<bool> DeleteTaskAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> EnqueueTaskAsync(string id, string queue, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    // ❌ OLD - Incorrect signature (extra `queue` parameter)
    // public Task<bool> DequeueTaskAsync(string id, string queue, CancellationToken cancellationToken = default)

    // ✅ NEW - Corrected to match `IClearMLService`
    public Task<bool> DequeueTaskAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> StopTaskAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<IReadOnlyList<ClearMLTask>> GetTasksForQueueAsync(string queue, CancellationToken cancellationToken)
    {
        if (ThrowException)
            throw new InvalidOperationException("ClearML encountered an error while retrieving tasks.");

        // Simulate ClearML tasks in queue
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

        return Task.FromResult<IReadOnlyList<ClearMLTask>>(fakeTasks);
    }

    public Task<ClearMLTask?> GetTaskByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<ClearMLTask?>(null);

    public Task<IReadOnlyList<ClearMLTask>> GetTasksByIdAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<IReadOnlyList<ClearMLTask>>(new List<ClearMLTask>());
}

// ============================
// Fake SharedFileService Implementation
// ============================

public class FakeSharedFileService : ISharedFileService
{
    public Uri GetBaseUri() => new Uri("https://fakefiles.com/");

    public Uri GetResolvedUri(string path) => new Uri($"https://fakefiles.com/{path}");

    public Task<string> GetDownloadUrlAsync(string path, DateTime expiresAt) =>
        Task.FromResult($"https://fakefiles.com/{path}?expires={expiresAt:o}");

    public Task<IReadOnlyCollection<string>> ListFilesAsync(
        string path,
        bool recurse = false,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<IReadOnlyCollection<string>>(new List<string> { $"{path}/file1.txt", $"{path}/file2.txt" });

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<Stream> OpenWriteAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

// ============================
// Fake Logger Implementation
// ============================

public class FakeLogger<T> : ILogger<T>
{
    public bool HasError { get; private set; }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception, // Make exception nullable
        Func<TState, Exception?, string> formatter // Make exception nullable here too
    )
    {
        if (logLevel == LogLevel.Error)
            HasError = true;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;
}

public sealed class Disposable : IDisposable
{
    public static readonly IDisposable Empty = new Disposable();

    private Disposable() { }

    public void Dispose() { }
}

// ============================
// Testable subclass for ClearMLMonitorService
// ============================

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
