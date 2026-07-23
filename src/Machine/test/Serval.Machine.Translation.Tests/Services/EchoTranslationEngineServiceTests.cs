namespace Serval.Machine.Translation.Services;

[TestFixture]
public class EchoTranslationEngineServiceTests
{
    private const string EngineId1 = "engine1";
    private const string EngineId2 = "engine2";
    private const string BuildId1 = "build1";

    [Test]
    public async Task CreateAsync()
    {
        using var env = new TestEnvironment();
        await env.Service.CreateAsync(EngineId2, "en", "en", "Engine 2");
        TranslationEngine? engine = await env.Engines.GetAsync(e => e.EngineId == EngineId2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine, Is.Not.Null);
            Assert.That(engine?.EngineId, Is.EqualTo(EngineId2));
            Assert.That(engine?.BuildRevision, Is.EqualTo(0));
            Assert.That(engine?.IsModelPersisted, Is.False);
        }
    }

    [Test]
    public async Task StartBuildAsync()
    {
        using var env = new TestEnvironment();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        await env.Service.StartBuildAsync(EngineId1, BuildId1, [], "{}");
        await env.WaitForBuildToFinishAsync();
        engine = env.Engines.Get(EngineId1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.CurrentBuild, Is.Null);
            Assert.That(engine.BuildRevision, Is.EqualTo(2));
            Assert.That(engine.IsModelPersisted, Is.True);
        }
    }

    [Test]
    public async Task CancelBuildAsync_Building()
    {
        using var env = new TestEnvironment();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, [], "{}");
        await env.Service.CancelBuildAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Null);
    }

    [Test]
    public async Task CancelBuildAsync_NotBuilding()
    {
        using var env = new TestEnvironment();
        Assert.That(await env.Service.CancelBuildAsync(EngineId1), Is.Null);
    }

    [Test]
    public async Task DeleteAsync_WhileBuilding()
    {
        using var env = new TestEnvironment();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, [], "{}");
        await env.WaitForTrainingToStartAsync();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.DeleteAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        Assert.That(env.Engines.Contains(EngineId1), Is.False);
    }

    [Test]
    public async Task UpdateAsync()
    {
        using var env = new TestEnvironment();
        await env.Service.UpdateAsync(EngineId1, "fr", "fr");
        TranslationEngine engine = env.Engines.Get(EngineId1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.SourceLanguage, Is.EqualTo("fr"));
            Assert.That(engine.TargetLanguage, Is.EqualTo("fr"));
        }
    }

    [Test]
    public async Task TrainSegmentPairAsync()
    {
        using var env = new TestEnvironment();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, [], "{}");
        await env.WaitForBuildToStartAsync();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.TrainSegmentPairAsync(EngineId1, "esto es una prueba.", "this is a test.", true);
        await env.WaitForBuildToFinishAsync();
        engine = env.Engines.Get(EngineId1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(engine.CurrentBuild, Is.Null);
            Assert.That(engine.BuildRevision, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task TranslateAsync()
    {
        using var env = new TestEnvironment();
        TranslationResultContract result = (await env.Service.TranslateAsync(EngineId1, n: 1, "this is a test."))[0];
        Assert.That(result.Translation, Is.EqualTo("this is a test."));
    }

    [Test]
    public async Task GetWordGraphAsync()
    {
        using var env = new TestEnvironment();
        WordGraphContract result = await env.Service.GetWordGraphAsync(EngineId1, "this is a test.");
        Assert.That(result.Arcs.Select(a => string.Join(' ', a.TargetTokens)), Is.EqualTo(["this", "is", "a"]));
    }

    [Test]
    public async Task GetLanguageInfoAsync()
    {
        using var env = new TestEnvironment();
        LanguageInfoContract info = await env.Service.GetLanguageInfoAsync("en");
        Assert.That(info.InternalCode, Is.EqualTo("en_echo"));
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IBuildJobService<TranslationEngine>? _deferredBuildJobService;
        private readonly CancellationTokenSource _runnerCts = new();

        public TestEnvironment()
        {
            Engines = new MemoryRepository<TranslationEngine>();
            Engines.Add(
                new TranslationEngine
                {
                    Id = EngineId1,
                    EngineId = EngineId1,
                    Type = EngineType.Echo,
                    SourceLanguage = "en",
                    TargetLanguage = "en",
                    BuildRevision = 1,
                    IsModelPersisted = true,
                }
            );
            var platformService = Substitute.For<IPlatformService>();
            platformService.EngineGroup.Returns(EngineGroup.Translation);

            IOptionsMonitor<BuildJobOptions> buildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
            buildJobOptions.CurrentValue.Returns(new BuildJobOptions());

            var services = new ServiceCollection();
            services.AddScoped(_ => _deferredBuildJobService!);
            services.AddKeyedSingleton(EngineGroup.Translation, (_, _) => platformService);
            services.AddSingleton<IRepository<TranslationEngine>>(Engines);
            services.AddScoped<IDataAccessContext>(_ => new MemoryDataAccessContext());
            services.AddSingleton(Substitute.For<ISharedFileService>());
            services.AddSingleton(Substitute.For<IParallelCorpusService>());
            services.AddSingleton(buildJobOptions);
            services.AddSingleton(Substitute.For<ITranslationPlatformService>());
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            var jobRunner = new TranslationEngineLocalBuildJobRunner(
                [new EchoLocalBuildJobFactory()],
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _serviceProvider.GetRequiredService<ILogger<TranslationEngineLocalBuildJobRunner>>()
            );
            var buildJobService = new BuildJobService<TranslationEngine>([jobRunner], Engines);
            _deferredBuildJobService = buildJobService;
            _ = jobRunner.StartAsync(_runnerCts.Token);
            Service = new EchoTranslationEngineService(platformService, Engines, buildJobService);
        }

        public EchoTranslationEngineService Service { get; }
        public MemoryRepository<TranslationEngine> Engines { get; }

        public Task WaitForBuildToFinishAsync() => WaitForBuildState(e => e.CurrentBuild is null);

        public Task WaitForBuildToStartAsync() =>
            WaitForBuildState(e => e.CurrentBuild!.JobState is BuildJobState.Active);

        public Task WaitForTrainingToStartAsync() =>
            WaitForBuildState(e =>
                e.CurrentBuild!.JobState is BuildJobState.Active && e.CurrentBuild!.Stage is BuildStage.Train
            );

        private async Task WaitForBuildState(Func<TranslationEngine, bool> predicate)
        {
            using ISubscription<TranslationEngine> subscription = await Engines.SubscribeAsync(e =>
                e.EngineId == EngineId1
            );
            while (true)
            {
                TranslationEngine? engine = subscription.Change.Entity;
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
