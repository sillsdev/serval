namespace Serval.Machine.WordAlignment.Services;

[TestFixture]
public class EchoWordAlignmentEngineServiceTests
{
    private const string EngineId1 = "engine1";
    private const string EngineId2 = "engine2";
    private const string BuildId1 = "build1";

    [Test]
    public async Task CreateAsync()
    {
        using var env = new TestEnvironment();
        await env.Service.CreateAsync(EngineId2, "en", "en", "Engine 2");
        WordAlignmentEngine? engine = await env.Engines.GetAsync(e => e.EngineId == EngineId2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine, Is.Not.Null);
            Assert.That(engine?.EngineId, Is.EqualTo(EngineId2));
            Assert.That(engine?.BuildRevision, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task StartBuildAsync()
    {
        using var env = new TestEnvironment();
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        await env.Service.StartBuildAsync(EngineId1, BuildId1, [], "{}");
        await env.WaitForBuildToFinishAsync();
        engine = env.Engines.Get(EngineId1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.CurrentBuild, Is.Null);
            Assert.That(engine.BuildRevision, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task CancelBuildAsync_Building()
    {
        using var env = new TestEnvironment();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, [], "{}");
        await env.Service.CancelBuildAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Null);
    }

    [Test]
    public async Task CancelBuildAsync_NotBuilding()
    {
        using var env = new TestEnvironment();
        Assert.That(await env.Service.CancelBuildAsync(EngineId1), Is.Null);
    }

    public async Task DeleteAsync_WhileBuilding()
    {
        using var env = new TestEnvironment();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, [], "{}");
        await env.WaitForTrainingToStartAsync();
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.DeleteAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        Assert.That(env.Engines.Contains(EngineId1), Is.False);
    }

    [Test]
    public async Task AlignAsync()
    {
        using var env = new TestEnvironment();
        WordAlignmentResultContract result = await env.Service.AlignAsync(
            EngineId1,
            "esto es una prueba.",
            "this is a test."
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(string.Join(' ', result.TargetTokens), Is.EqualTo("this is a test."));
            Assert.That(result.Alignment[0].SourceIndex, Is.Zero);
            Assert.That(result.Alignment[0].TargetIndex, Is.Zero);
        }
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IBuildJobService<WordAlignmentEngine>? _deferredBuildJobService;
        private readonly CancellationTokenSource _runnerCts = new();

        public TestEnvironment()
        {
            Engines = new MemoryRepository<WordAlignmentEngine>();
            Engines.Add(
                new WordAlignmentEngine
                {
                    Id = EngineId1,
                    EngineId = EngineId1,
                    Type = EngineType.EchoWordAlignment,
                    SourceLanguage = "en",
                    TargetLanguage = "en",
                    BuildRevision = 1,
                }
            );
            var platformService = Substitute.For<IPlatformService>();
            platformService.EngineGroup.Returns(EngineGroup.WordAlignment);

            IOptionsMonitor<BuildJobOptions> buildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
            buildJobOptions.CurrentValue.Returns(new BuildJobOptions());

            var services = new ServiceCollection();
            services.AddScoped(_ => _deferredBuildJobService!);
            services.AddKeyedSingleton(EngineGroup.WordAlignment, (_, _) => platformService);
            services.AddSingleton<IRepository<WordAlignmentEngine>>(Engines);
            services.AddScoped<IDataAccessContext>(_ => new MemoryDataAccessContext());
            services.AddSingleton(Substitute.For<ISharedFileService>());
            services.AddSingleton(Substitute.For<IParallelCorpusService>());
            services.AddSingleton(buildJobOptions);
            services.AddSingleton(Substitute.For<IWordAlignmentPlatformService>());
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            var jobRunner = new WordAlignmentEngineLocalBuildJobRunner(
                [new EchoWordAlignmentLocalBuildJobFactory()],
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _serviceProvider.GetRequiredService<ILogger<WordAlignmentEngineLocalBuildJobRunner>>()
            );
            var buildJobService = new BuildJobService<WordAlignmentEngine>([jobRunner], Engines);
            _deferredBuildJobService = buildJobService;
            _ = jobRunner.StartAsync(_runnerCts.Token);
            Service = new EchoWordAlignmentEngineService(platformService, Engines, buildJobService);
        }

        public EchoWordAlignmentEngineService Service { get; }
        public MemoryRepository<WordAlignmentEngine> Engines { get; }

        public Task WaitForBuildToFinishAsync() => WaitForBuildState(e => e.CurrentBuild is null);

        public Task WaitForTrainingToStartAsync() =>
            WaitForBuildState(e =>
                e.CurrentBuild!.JobState is BuildJobState.Active && e.CurrentBuild!.Stage is BuildStage.Train
            );

        private async Task WaitForBuildState(Func<WordAlignmentEngine, bool> predicate)
        {
            using ISubscription<WordAlignmentEngine> subscription = await Engines.SubscribeAsync(e =>
                e.EngineId == EngineId1
            );
            while (true)
            {
                WordAlignmentEngine? engine = subscription.Change.Entity;
                if (engine is null || predicate(engine))
                    break;
                await subscription.WaitForChangeAsync();
            }
        }

        protected override void DisposeManagedResources()
        {
            _runnerCts.Cancel();
            _serviceProvider.Dispose();
            _runnerCts.Dispose();
        }
    }
}
