namespace Serval.Machine.Translation.Services;

[TestFixture]
public class TranslationEngineLocalBuildJobRunnerTests
{
    [Test]
    public async Task FaultsAreLogged()
    {
        using var env = new TestEnvironment();

        // SUT
        await env.StartAsync();
        await Task.Delay(100);
        await env.CancelAsync();

        // Verify exceptions were logged
        env.Logger.Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString()!.Contains("Exception while executing task on local build runner")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()!
            );
        env.Logger.Received()
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString()!.Contains("Exception while executing local build runner")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()!
            );
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly TranslationEngineLocalBuildJobRunner _jobRunner;
        private readonly CancellationTokenSource _runnerCts = new();
        private readonly ServiceProvider _serviceProvider;

        public TestEnvironment()
        {
            var platformService = Substitute.For<IPlatformService>();
            platformService.EngineGroup.Returns(EngineGroup.Translation);
            var engines = new MemoryRepository<TranslationEngine>();
            var services = new ServiceCollection();
            services.AddKeyedSingleton(EngineGroup.Translation, (_, _) => platformService);
            services.AddScoped<IBuildJobService<TranslationEngine>>(_ => new BuildJobService<TranslationEngine>(
                [],
                engines
            ));
            services.AddScoped<IDataAccessContext>(_ => new MemoryDataAccessContext());
            services.AddSingleton<IRepository<TranslationEngine>>(engines);
            _serviceProvider = services.BuildServiceProvider();
            Logger = Substitute.For<ILogger<TranslationEngineLocalBuildJobRunner>>();
            _jobRunner = new FaultedTranslationEngineLocalBuildJobRunner(
                factories: [],
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                Logger
            );
        }

        public ILogger<TranslationEngineLocalBuildJobRunner> Logger { get; }

        public Task StartAsync() => _jobRunner.StartAsync(_runnerCts.Token);

        public Task CancelAsync() => _runnerCts.CancelAsync();

        protected override void DisposeManagedResources()
        {
            _runnerCts.Cancel();
            _serviceProvider.Dispose();
            _runnerCts.Dispose();
        }
    }

    private class FaultedTranslationEngineLocalBuildJobRunner(
        IEnumerable<ILocalBuildJobFactory> factories,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TranslationEngineLocalBuildJobRunner> logger
    ) : TranslationEngineLocalBuildJobRunner(factories, serviceScopeFactory, logger)
    {
        protected override Task ProcessJobsAsync(EngineGroup engineGroup, CancellationToken stoppingToken) =>
            Task.FromException(new InvalidOperationException());
    }
}
