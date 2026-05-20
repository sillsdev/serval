namespace Serval.Machine.Shared.Services;

[TestFixture]
public class NmtEngineServiceTests
{
    [Test]
    public async Task StartBuildAsync()
    {
        using var env = new TestEnvironment();
        env.PersistModel();
        TranslationEngine engine = env.Engines.Get("engine1");
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        await env.Service.StartBuildAsync("engine1", "build1", Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForBuildToFinishAsync();
        engine = env.Engines.Get("engine1");
        Assert.Multiple(() =>
        {
            Assert.That(engine.CurrentBuild, Is.Null);
            Assert.That(engine.BuildRevision, Is.EqualTo(2));
            Assert.That(engine.IsModelPersisted, Is.True);
        });
    }

    [Test]
    public async Task CancelBuildAsync_Building()
    {
        using var env = new TestEnvironment();
        env.PersistModel();
        env.UseInfiniteTrainJob();

        TranslationEngine engine = env.Engines.Get("engine1");
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        await env.Service.StartBuildAsync("engine1", "build1", Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForBuildToStartAsync();
        engine = env.Engines.Get("engine1");
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.CancelBuildAsync("engine1");
        await env.WaitForBuildToFinishAsync();
        engine = env.Engines.Get("engine1");
        Assert.That(engine.CurrentBuild, Is.Null);
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
    }

    [Test]
    public async Task CancelBuildAsync_NotBuilding()
    {
        using var env = new TestEnvironment();
        Assert.That(await env.Service.CancelBuildAsync("engine1"), Is.Null);
    }

    [Test]
    public async Task DeleteAsync_WhileBuilding()
    {
        using var env = new TestEnvironment();
        env.PersistModel();
        env.UseInfiniteTrainJob();

        TranslationEngine engine = env.Engines.Get("engine1");
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        await env.Service.StartBuildAsync("engine1", "build1", Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForBuildToStartAsync();
        engine = env.Engines.Get("engine1");
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.DeleteAsync("engine1");
        // ensure that the train job has completed
        await env.WaitForBuildToFinishAsync();
        Assert.That(env.Engines.Contains("engine1"), Is.False);
    }

    [Test]
    public async Task UpdateAsync()
    {
        using var env = new TestEnvironment();
        await env.Service.UpdateAsync("engine1", "fr", "en");
        TranslationEngine engine = env.Engines.Get("engine1");
        Assert.That(engine.SourceLanguage, Is.EqualTo("fr"));
        Assert.That(engine.TargetLanguage, Is.EqualTo("en"));
    }

    [Test]
    public async Task GetLanguageInfoAsync()
    {
        using var env = new TestEnvironment();
        LanguageInfoContract info = await env.Service.GetLanguageInfoAsync("en");
        Assert.That(info.InternalCode, Is.EqualTo("eng_Latn"));
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly LocalBuildJobRunner _jobRunner;
        private readonly CancellationTokenSource _runnerCts = new();
        private readonly ServiceProvider _serviceProvider;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Func<Task> _trainJobFunc;
        private Task? _trainJobTask;

        public TestEnvironment()
        {
            if (!Sldr.IsInitialized)
                Sldr.Initialize(offlineTestMode: true);

            _trainJobFunc = RunNormalTrainJob;
            Engines = new MemoryRepository<TranslationEngine>();
            Engines.Add(
                new TranslationEngine
                {
                    Id = "engine1",
                    EngineId = "engine1",
                    Type = EngineType.Nmt,
                    SourceLanguage = "es",
                    TargetLanguage = "en",
                    BuildRevision = 1,
                    IsModelPersisted = false,
                }
            );
            PlatformService = Substitute.For<IPlatformService>();
            PlatformService.EngineGroup.Returns(EngineGroup.Translation);
            TranslationPlatformService = Substitute.For<ITranslationPlatformService>();
            ClearMLService = Substitute.For<IClearMLService>();
            ClearMLService
                .GetProjectIdAsync("engine1", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("project1"));
            ClearMLService
                .CreateTaskAsync(
                    "build1",
                    "project1",
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Task.FromResult("job1"));
            ClearMLService
                .When(x => x.EnqueueTaskAsync("job1", Arg.Any<string>(), Arg.Any<CancellationToken>()))
                .Do(_ => _trainJobTask = Task.Run(_trainJobFunc));
            ClearMLService
                .When(x => x.StopTaskAsync("job1", Arg.Any<CancellationToken>()))
                .Do(_ => _cancellationTokenSource.Cancel());
            SharedFileService = new SharedFileService(Substitute.For<ILoggerFactory>());
            BuildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
            BuildJobOptions.CurrentValue.Returns(
                new BuildJobOptions
                {
                    ClearML =
                    [
                        new ClearMLBuildQueue()
                        {
                            EngineType = EngineType.Nmt,
                            ModelType = "huggingface",
                            DockerImage = "default",
                            Queue = "default",
                        },
                        new ClearMLBuildQueue()
                        {
                            EngineType = EngineType.SmtTransfer,
                            ModelType = "thot",
                            DockerImage = "default",
                            Queue = "default",
                        },
                    ],
                }
            );

            IBuildJobService<TranslationEngine>? deferredBuildJobService = null;
            var services = new ServiceCollection();
            services.AddScoped(_ => deferredBuildJobService!);
            services.AddSingleton(Substitute.For<IBuildJobService<WordAlignmentEngine>>());
            services.AddKeyedSingleton(EngineGroup.Translation, (_, _) => PlatformService);
            services.AddKeyedSingleton(EngineGroup.WordAlignment, (_, _) => Substitute.For<IPlatformService>());
            services.AddSingleton<IRepository<TranslationEngine>>(Engines);
            services.AddSingleton<IRepository<WordAlignmentEngine>>(new MemoryRepository<WordAlignmentEngine>());
            services.AddScoped<IDataAccessContext>(_ => new MemoryDataAccessContext());
            services.AddSingleton(SharedFileService);
            services.AddSingleton<ILanguageTagService>(new LanguageTagService());
            services.AddSingleton(Substitute.For<IParallelCorpusService>());
            services.AddSingleton(BuildJobOptions);
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            _jobRunner = new LocalBuildJobRunner(
                [new NmtLocalBuildJobFactory()],
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _serviceProvider.GetRequiredService<ILogger<LocalBuildJobRunner>>()
            );

            var clearMLOptions = Substitute.For<IOptionsMonitor<ClearMLOptions>>();
            clearMLOptions.CurrentValue.Returns(new ClearMLOptions());
            ClearMLQueueService = new ClearMLMonitorService(
                Substitute.For<IServiceProvider>(),
                ClearMLService,
                SharedFileService,
                clearMLOptions,
                BuildJobOptions,
                Substitute.For<ILogger<ClearMLMonitorService>>()
            );
            BuildJobService = new BuildJobService<TranslationEngine>(
                [
                    _jobRunner,
                    new ClearMLBuildJobRunner(
                        ClearMLService,
                        [
                            new NmtClearMLBuildJobFactory(
                                SharedFileService,
                                Substitute.For<ILanguageTagService>(),
                                Engines
                            ),
                        ],
                        BuildJobOptions
                    ),
                ],
                Engines
            );
            deferredBuildJobService = BuildJobService;
            _ = _jobRunner.StartAsync(_runnerCts.Token);
            Service = CreateService();
        }

        public NmtEngineService Service { get; private set; }
        public IClearMLQueueService ClearMLQueueService { get; }
        public MemoryRepository<TranslationEngine> Engines { get; }
        public IPlatformService PlatformService { get; }
        public ITranslationPlatformService TranslationPlatformService { get; }
        public IClearMLService ClearMLService { get; }
        public ISharedFileService SharedFileService { get; }
        public IBuildJobService<TranslationEngine> BuildJobService { get; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }

        public void PersistModel()
        {
            Engines.Replace(Engines.Get("engine1") with { IsModelPersisted = true });
        }

        private NmtEngineService CreateService()
        {
            return new NmtEngineService(
                TranslationPlatformService,
                Engines,
                BuildJobService,
                new LanguageTagService(),
                ClearMLQueueService,
                SharedFileService
            );
        }

        public async Task WaitForBuildToFinishAsync()
        {
            await WaitForBuildState(e => e.CurrentBuild is null);
            if (_trainJobTask is not null)
                await _trainJobTask;
        }

        public Task WaitForBuildToStartAsync()
        {
            return WaitForBuildState(e =>
                e.CurrentBuild!.JobState is BuildJobState.Active && e.CurrentBuild!.Stage == BuildStage.Train
            );
        }

        public void UseInfiniteTrainJob()
        {
            _trainJobFunc = RunInfiniteTrainJob;
        }

        private async Task WaitForBuildState(Func<TranslationEngine, bool> predicate)
        {
            using ISubscription<TranslationEngine> subscription = await Engines.SubscribeAsync(e =>
                e.EngineId == "engine1"
            );
            while (true)
            {
                TranslationEngine? engine = subscription.Change.Entity;
                if (engine is null || predicate(engine))
                    break;
                await subscription.WaitForChangeAsync();
            }
        }

        private async Task RunNormalTrainJob()
        {
            await BuildJobService.BuildJobStartedAsync("engine1", "build1");

            await using Stream stream = await SharedFileService.OpenWriteAsync("builds/build1/pretranslate.trg.json");

            await BuildJobService.StartBuildJobAsync(
                BuildJobRunnerType.Local,
                EngineType.Nmt,
                "engine1",
                "build1",
                BuildStage.Postprocess,
                (0, 0.0)
            );
        }

        private async Task RunInfiniteTrainJob()
        {
            await BuildJobService.BuildJobStartedAsync("engine1", "build1");

            while (!_cancellationTokenSource.IsCancellationRequested)
                await Task.Delay(50);

            await BuildJobService.BuildJobFinishedAsync("engine1", "build1", buildComplete: false);
        }

        protected override void DisposeManagedResources()
        {
            _runnerCts.Cancel();
            _serviceProvider.Dispose();
            _cancellationTokenSource.Dispose();
            _runnerCts.Dispose();
        }
    }
}
