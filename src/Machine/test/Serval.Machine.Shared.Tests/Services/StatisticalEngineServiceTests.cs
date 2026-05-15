using Serval.WordAlignment.Contracts;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class StatisticalEngineServiceTests
{
    const string EngineId1 = "engine1";
    const string EngineId2 = "engine2";
    const string BuildId1 = "build1";
    const string CorpusId1 = "corpus1";

    [Test]
    public async Task CreateAsync()
    {
        using var env = new TestEnvironment();
        await env.Service.CreateAsync(EngineId2, "es", "en", "Engine 2");
        WordAlignmentEngine? engine = await env.Engines.GetAsync(e => e.EngineId == EngineId2);
        Assert.Multiple(() =>
        {
            Assert.That(engine, Is.Not.Null);
            Assert.That(engine?.EngineId, Is.EqualTo(EngineId2));
            Assert.That(engine?.BuildRevision, Is.EqualTo(0));
        });
        string engineDir = Path.Combine("word_alignment_engines", EngineId2);
        env.WordAlignmentModelFactory.Received().InitNew(engineDir);
    }

    [TestCase(BuildJobRunnerType.Local)]
    [TestCase(BuildJobRunnerType.ClearML)]
    public async Task StartBuildAsync(BuildJobRunnerType trainJobRunnerType)
    {
        using var env = new TestEnvironment(trainJobRunnerType);
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        // ensure that the model was loaded before training
        await env.Service.AlignAsync(EngineId1, "esto es una prueba.", "this is a test.");
        await env.Service.StartBuildAsync(
            EngineId1,
            BuildId1,
            [
                new ParallelCorpusContract()
                {
                    Id = CorpusId1,
                    SourceCorpora = new List<MonolingualCorpusContract>()
                    {
                        new()
                        {
                            Id = "src",
                            Language = "es",
                            Files = [],
                            TrainOnTextIds = null,
                            InferenceTextIds = null,
                        },
                    },
                    TargetCorpora = new List<MonolingualCorpusContract>()
                    {
                        new()
                        {
                            Id = "trg",
                            Language = "en",
                            Files = [],
                            TrainOnTextIds = null,
                        },
                    },
                },
            ]
        );
        await env.WaitForBuildToFinishAsync();
        _ = env
            .WordAlignmentBatchTrainer.Received()
            .TrainAsync(Arg.Any<IProgress<ProgressStatus>>(), Arg.Any<CancellationToken>());
        _ = env.WordAlignmentBatchTrainer.Received().SaveAsync(Arg.Any<CancellationToken>());
        engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Null);
        Assert.That(engine.BuildRevision, Is.EqualTo(2));
        // check if model was reloaded upon first use after training
        env.WordAlignmentModel.ClearReceivedCalls();
        await env.Service.AlignAsync(EngineId1, "esto es una prueba.", "this is a test.");
        env.WordAlignmentModel.Received().Dispose();
    }

    [TestCase(BuildJobRunnerType.Local)]
    [TestCase(BuildJobRunnerType.ClearML)]
    public async Task CancelBuildAsync_Building(BuildJobRunnerType trainJobRunnerType)
    {
        using var env = new TestEnvironment(trainJobRunnerType);
        env.UseInfiniteTrainJob();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForTrainingToStartAsync();
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.CancelBuildAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        _ = env.WordAlignmentBatchTrainer.DidNotReceive().SaveAsync();
        engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Null);
    }

    [Test]
    public async Task CancelBuildAsync_NotBuilding()
    {
        using var env = new TestEnvironment();
        Assert.That(await env.Service.CancelBuildAsync(EngineId1), Is.Null);
    }

    [TestCase(BuildJobRunnerType.Local)]
    [TestCase(BuildJobRunnerType.ClearML)]
    public async Task DeleteAsync_WhileBuilding(BuildJobRunnerType trainJobRunnerType)
    {
        using var env = new TestEnvironment(trainJobRunnerType);
        env.UseInfiniteTrainJob();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForTrainingToStartAsync();
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.DeleteAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        _ = env.WordAlignmentBatchTrainer.DidNotReceive().SaveAsync();
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
        Assert.That(string.Join(' ', result.TargetTokens), Is.EqualTo("this is a test ."));
        Assert.That(result.Alignment[0].SourceIndex, Is.EqualTo(0));
        Assert.That(result.Alignment[0].TargetIndex, Is.EqualTo(0));
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IDistributedReaderWriterLockFactory _lockFactory;
        private readonly BuildJobRunnerType _trainJobRunnerType;
        private readonly ClearMLBuildJobRunner _clearMLRunner;
        private readonly ServiceProvider _serviceProvider;
        private IBuildJobService<WordAlignmentEngine>? _deferredBuildJobService;
        private LocalBuildJobRunner _jobRunner;
        private CancellationTokenSource _runnerCts = new();
        private Task? _trainJobTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _training = true;

        public TestEnvironment(BuildJobRunnerType trainJobRunnerType = BuildJobRunnerType.ClearML)
        {
            _trainJobRunnerType = trainJobRunnerType;
            Engines = new MemoryRepository<WordAlignmentEngine>();
            Engines.Add(
                new WordAlignmentEngine
                {
                    Id = EngineId1,
                    EngineId = EngineId1,
                    Type = EngineType.Statistical,
                    SourceLanguage = "es",
                    TargetLanguage = "en",
                    BuildRevision = 1,
                }
            );
            PlatformService = Substitute.For<IPlatformService>();
            PlatformService.EngineGroup.Returns(EngineGroup.WordAlignment);
            WordAlignmentModel = Substitute.For<IWordAlignmentModel>();
            WordAlignmentBatchTrainer = Substitute.For<ITrainer>();
            WordAlignmentBatchTrainer.Stats.Returns(new TrainStats { TrainCorpusSize = 0 });
            WordAlignmentModelFactory = CreateWordAlignmentModelFactory();
            _lockFactory = new DistributedReaderWriterLockFactory(
                new OptionsWrapper<ServiceOptions>(new ServiceOptions { ServiceId = "host" }),
                new OptionsWrapper<DistributedReaderWriterLockOptions>(new DistributedReaderWriterLockOptions()),
                new MemoryRepository<RWLock>(),
                new ObjectIdGenerator()
            );
            SharedFileService = new SharedFileService(Substitute.For<ILoggerFactory>());
            var clearMLOptions = Substitute.For<IOptionsMonitor<ClearMLOptions>>();
            clearMLOptions.CurrentValue.Returns(new ClearMLOptions());
            BuildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
            BuildJobOptions.CurrentValue.Returns(
                new BuildJobOptions
                {
                    ClearML =
                    [
                        new ClearMLBuildQueue()
                        {
                            EngineType = EngineType.Statistical,
                            ModelType = "thot",
                            DockerImage = "default",
                            Queue = "default",
                        },
                    ],
                }
            );
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
                .Do(_ => _trainJobTask = Task.Run(RunTrainJob));
            ClearMLService
                .When(x => x.StopTaskAsync("job1", Arg.Any<CancellationToken>()))
                .Do(_ => _cancellationTokenSource.Cancel());
            ClearMLMonitorService = new ClearMLMonitorService(
                Substitute.For<IServiceProvider>(),
                ClearMLService,
                SharedFileService,
                clearMLOptions,
                BuildJobOptions,
                Substitute.For<ILogger<ClearMLMonitorService>>()
            );

            _clearMLRunner = new ClearMLBuildJobRunner(
                ClearMLService,
                [new StatisticalClearMLBuildJobFactory(SharedFileService, Engines)],
                BuildJobOptions
            );

            var statisticalEngineOptions = Substitute.For<IOptionsMonitor<StatisticalEngineOptions>>();
            statisticalEngineOptions.CurrentValue.Returns(new StatisticalEngineOptions());

            var services = new ServiceCollection();
            services.AddScoped(_ => _deferredBuildJobService!);
            services.AddSingleton(Substitute.For<IBuildJobService<TranslationEngine>>());
            services.AddKeyedSingleton(EngineGroup.WordAlignment, (_, _) => PlatformService);
            services.AddKeyedSingleton(EngineGroup.Translation, (_, _) => Substitute.For<IPlatformService>());
            services.AddSingleton<IRepository<TranslationEngine>>(new MemoryRepository<TranslationEngine>());
            services.AddSingleton<IRepository<WordAlignmentEngine>>(Engines);
            services.AddScoped<IDataAccessContext>(_ => new MemoryDataAccessContext());
            services.AddSingleton(SharedFileService);
            services.AddSingleton(_lockFactory);
            services.AddSingleton(Substitute.For<IParallelCorpusService>());
            services.AddSingleton(BuildJobOptions);
            services.AddSingleton(WordAlignmentModelFactory);
            services.AddSingleton(statisticalEngineOptions);
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            _jobRunner = CreateJobRunner();
            BuildJobService = CreateBuildJobService();
            _deferredBuildJobService = BuildJobService;
            StateService = CreateStateService();
            Service = CreateService();
            _ = _jobRunner.StartAsync(_runnerCts.Token);
        }

        public StatisticalEngineService Service { get; private set; }
        public StatisticalEngineStateService StateService { get; private set; }
        public MemoryRepository<WordAlignmentEngine> Engines { get; }
        public IWordAlignmentModelFactory WordAlignmentModelFactory { get; }
        public ITrainer WordAlignmentBatchTrainer { get; }
        public IWordAlignmentModel WordAlignmentModel { get; }
        public IPlatformService PlatformService { get; }
        public IClearMLService ClearMLService { get; }
        public IClearMLQueueService ClearMLMonitorService { get; }
        public ISharedFileService SharedFileService { get; }
        public IBuildJobService<WordAlignmentEngine> BuildJobService { get; private set; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }

        public async Task CommitAsync(TimeSpan inactiveTimeout)
        {
            await StateService.CommitAsync(_lockFactory, Engines, inactiveTimeout);
        }

        public void StopServer()
        {
            _runnerCts.Cancel();
            StateService.Dispose();
        }

        public void StartServer()
        {
            _runnerCts.Dispose();
            _runnerCts = new CancellationTokenSource();
            _jobRunner = CreateJobRunner();
            BuildJobService = CreateBuildJobService();
            _deferredBuildJobService = BuildJobService;
            StateService = CreateStateService();
            Service = CreateService();
            _ = _jobRunner.StartAsync(_runnerCts.Token);
        }

        public void UseInfiniteTrainJob()
        {
            WordAlignmentBatchTrainer.TrainAsync(
                Arg.Any<IProgress<ProgressStatus>>(),
                Arg.Do<CancellationToken>(cancellationToken =>
                {
                    while (_training)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Thread.Sleep(100);
                    }
                })
            );
        }

        public void StopTraining()
        {
            _training = false;
        }

        private LocalBuildJobRunner CreateJobRunner()
        {
            return new LocalBuildJobRunner(
                [new StatisticalTestLocalBuildJobFactory(_trainJobRunnerType)],
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _serviceProvider.GetRequiredService<ILogger<LocalBuildJobRunner>>()
            );
        }

        private IBuildJobService<WordAlignmentEngine> CreateBuildJobService()
        {
            return new BuildJobService<WordAlignmentEngine>([_jobRunner, _clearMLRunner], Engines);
        }

        private StatisticalEngineStateService CreateStateService()
        {
            var options = Substitute.For<IOptionsMonitor<StatisticalEngineOptions>>();
            options.CurrentValue.Returns(new StatisticalEngineOptions());
            return new StatisticalEngineStateService(
                WordAlignmentModelFactory,
                options,
                Substitute.For<ILogger<StatisticalEngineStateService>>()
            );
        }

        private StatisticalEngineService CreateService()
        {
            return new StatisticalEngineService(
                _lockFactory,
                PlatformService,
                Engines,
                StateService,
                BuildJobService,
                ClearMLMonitorService
            );
        }

        private IWordAlignmentModelFactory CreateWordAlignmentModelFactory()
        {
            IWordAlignmentModelFactory factory = Substitute.For<IWordAlignmentModelFactory>();

            var alignedWordPair = new AlignedWordPair(0, 0);
            WordAlignmentModel
                .GetBestAlignedWordPairs(Arg.Any<List<string>>(), Arg.Any<List<string>>())
                .Returns([alignedWordPair, alignedWordPair, alignedWordPair, alignedWordPair, alignedWordPair]);
            factory.Create(Arg.Any<string>()).Returns(WordAlignmentModel);
            factory
                .CreateTrainer(
                    Arg.Any<string>(),
                    Arg.Any<IRangeTokenizer<string, int, string>>(),
                    Arg.Any<IParallelTextCorpus>()
                )
                .Returns(WordAlignmentBatchTrainer);
            return factory;
        }

        public async Task WaitForBuildToFinishAsync()
        {
            await WaitForBuildState(e => e.CurrentBuild is null);
            if (_trainJobTask is not null)
                await _trainJobTask;
        }

        public Task WaitForBuildToStartAsync()
        {
            return WaitForBuildState(e => e.CurrentBuild!.JobState is BuildJobState.Active);
        }

        public Task WaitForTrainingToStartAsync()
        {
            return WaitForBuildState(e =>
                e.CurrentBuild!.JobState is BuildJobState.Active && e.CurrentBuild!.Stage is BuildStage.Train
            );
        }

        public Task WaitForBuildToRestartAsync()
        {
            return WaitForBuildState(e => e.CurrentBuild!.JobState is BuildJobState.Pending);
        }

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
            StateService.Dispose();
            _runnerCts.Cancel();
            _serviceProvider.Dispose();
            _cancellationTokenSource.Dispose();
            _runnerCts.Dispose();
        }

        private async Task RunTrainJob()
        {
            try
            {
                await BuildJobService.BuildJobStartedAsync("engine1", "build1", _cancellationTokenSource.Token);

                string engineDir = Path.Combine("word_alignment_engines", EngineId1);
                WordAlignmentModelFactory.InitNew(engineDir);
                ITextCorpus sourceCorpus = new DictionaryTextCorpus();
                ITextCorpus targetCorpus = new DictionaryTextCorpus();
                IParallelTextCorpus parallelCorpus = sourceCorpus.AlignRows(targetCorpus);
                LatinWordTokenizer tokenizer = new();
                using ITrainer wordAlignmentModelTrainer = WordAlignmentModelFactory.CreateTrainer(
                    engineDir,
                    tokenizer,
                    parallelCorpus
                );
                await wordAlignmentModelTrainer.TrainAsync(null, _cancellationTokenSource.Token);
                await wordAlignmentModelTrainer.SaveAsync(_cancellationTokenSource.Token);

                await using Stream engineStream = await SharedFileService.OpenWriteAsync(
                    $"builds/{BuildId1}/model.tar.gz",
                    _cancellationTokenSource.Token
                );
                await using Stream targetStream = await SharedFileService.OpenWriteAsync(
                    $"builds/{BuildId1}/word_alignments.outputs.json",
                    _cancellationTokenSource.Token
                );

                await BuildJobService.StartBuildJobAsync(
                    BuildJobRunnerType.Local,
                    EngineType.Statistical,
                    EngineId1,
                    BuildId1,
                    BuildStage.Postprocess,
                    data: (0, 0.0)
                );
            }
            catch (OperationCanceledException)
            {
                await BuildJobService.BuildJobFinishedAsync("engine1", "build1", buildComplete: false);
            }
        }

        private class StatisticalTestLocalBuildJobFactory(BuildJobRunnerType trainJobRunnerType) : ILocalBuildJobFactory
        {
            private static readonly JsonSerializerOptions SerializerOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            public EngineType EngineType => EngineType.Statistical;

            public string? Serialize(BuildStage stage, object? data) =>
                new StatisticalLocalBuildJobFactory().Serialize(stage, data);

            public async Task RunAsync(
                IServiceProvider serviceProvider,
                string engineId,
                string buildId,
                BuildStage stage,
                string? jobData,
                string? buildOptions,
                CancellationToken cancellationToken
            )
            {
                switch (stage)
                {
                    case BuildStage.Preprocess:
                        var preprocessJob = ActivatorUtilities.CreateInstance<WordAlignmentPreprocessBuildJob>(
                            serviceProvider
                        );
                        preprocessJob.TrainJobRunnerType = trainJobRunnerType;
                        var corpora = JsonSerializer.Deserialize<List<ParallelCorpusContract>>(
                            jobData!,
                            SerializerOptions
                        )!;
                        await preprocessJob.RunAsync(engineId, buildId, corpora, buildOptions, cancellationToken);
                        break;
                    default:
                        await new StatisticalLocalBuildJobFactory().RunAsync(
                            serviceProvider,
                            engineId,
                            buildId,
                            stage,
                            jobData,
                            buildOptions,
                            cancellationToken
                        );
                        break;
                }
            }
        }
    }
}
