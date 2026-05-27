namespace Serval.Machine.Shared.Services;

[TestFixture]
public class SmtTransferEngineServiceTests
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
        TranslationEngine? engine = await env.Engines.GetAsync(e => e.EngineId == EngineId2);
        Assert.Multiple(() =>
        {
            Assert.That(engine, Is.Not.Null);
            Assert.That(engine?.EngineId, Is.EqualTo(EngineId2));
            Assert.That(engine?.BuildRevision, Is.EqualTo(0));
            Assert.That(engine?.IsModelPersisted, Is.True);
        });
        string engineDir = Path.Combine("translation_engines", EngineId2);
        env.SmtModelFactory.Received().InitNew(engineDir);
        env.TransferEngineFactory.Received().InitNew(engineDir);
    }

    public async Task StartBuildAsync()
    {
        using var env = new TestEnvironment();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        // ensure that the SMT model was loaded before training
        await env.Service.TranslateAsync(EngineId1, n: 1, "esto es una prueba.");
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
            .SmtBatchTrainer.Received()
            .TrainAsync(Arg.Any<IProgress<ProgressStatus>>(), Arg.Any<CancellationToken>());
        _ = env
            .TruecaserTrainer.Received()
            .TrainAsync(Arg.Any<IProgress<ProgressStatus>>(), Arg.Any<CancellationToken>());
        _ = env.SmtBatchTrainer.Received().SaveAsync(Arg.Any<CancellationToken>());
        _ = env.TruecaserTrainer.Received().SaveAsync(Arg.Any<CancellationToken>());
        engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Null);
        Assert.That(engine.BuildRevision, Is.EqualTo(2));
        // check if SMT model was reloaded upon first use after training
        env.SmtModel.ClearReceivedCalls();
        await env.Service.TranslateAsync(EngineId1, n: 1, "esto es una prueba.");
        env.SmtModel.Received().Dispose();
        _ = env.SmtModel.DidNotReceive().SaveAsync();
        _ = env.Truecaser.DidNotReceive().SaveAsync();
    }

    public async Task CancelBuildAsync_Building()
    {
        using var env = new TestEnvironment();
        env.UseInfiniteTrainJob();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForTrainingToStartAsync();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.CancelBuildAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        _ = env.SmtBatchTrainer.DidNotReceive().SaveAsync();
        _ = env.TruecaserTrainer.DidNotReceive().SaveAsync();
        engine = env.Engines.Get(EngineId1);
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
        env.UseInfiniteTrainJob();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForTrainingToStartAsync();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.DeleteAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        _ = env.SmtBatchTrainer.DidNotReceive().SaveAsync();
        _ = env.TruecaserTrainer.DidNotReceive().SaveAsync();
        Assert.That(env.Engines.Contains(EngineId1), Is.False);
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

    public async Task TrainSegmentPairAsync()
    {
        using var env = new TestEnvironment();
        env.UseInfiniteTrainJob();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, Array.Empty<ParallelCorpusContract>(), "{}");
        await env.WaitForBuildToStartAsync();
        TranslationEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.TrainSegmentPairAsync(EngineId1, "esto es una prueba.", "this is a test.", true);
        env.StopTraining();
        await env.WaitForBuildToFinishAsync();
        engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Null);
        Assert.That(engine.BuildRevision, Is.EqualTo(2));
        env.SmtModel.Received(2).TrainSegment("esto es una prueba.", "this is a test.", true);
    }

    [Test]
    public async Task CommitAsync_LoadedInactive()
    {
        using var env = new TestEnvironment();
        await env.Service.TrainSegmentPairAsync(EngineId1, "esto es una prueba.", "this is a test.", true);
        await Task.Delay(10);
        await env.CommitAsync(TimeSpan.Zero);
        env.SmtModel.Received().Save();
        Assert.That(env.StateService.Get(EngineId1).IsLoaded, Is.False);
    }

    [Test]
    public async Task CommitAsync_LoadedActive()
    {
        using var env = new TestEnvironment();
        await env.Service.TrainSegmentPairAsync(EngineId1, "esto es una prueba.", "this is a test.", true);
        await env.CommitAsync(TimeSpan.FromHours(1));
        env.SmtModel.Received().Save();
        Assert.That(env.StateService.Get(EngineId1).IsLoaded, Is.True);
    }

    [Test]
    public async Task TranslateAsync()
    {
        using var env = new TestEnvironment();
        TranslationResultContract result = (await env.Service.TranslateAsync(EngineId1, n: 1, "esto es una prueba."))[
            0
        ];
        Assert.That(result.Translation, Is.EqualTo("this is a TEST."));
    }

    [Test]
    public async Task GetWordGraphAsync()
    {
        using var env = new TestEnvironment();
        WordGraphContract result = await env.Service.GetWordGraphAsync(EngineId1, "esto es una prueba.");
        Assert.That(
            result.Arcs.Select(a => string.Join(' ', a.TargetTokens)),
            Is.EqualTo(new[] { "this is", "a test", "." })
        );
    }

    [Test]
    public async Task GetLanguageInfoAsync()
    {
        using var env = new TestEnvironment();
        LanguageInfoContract info = await env.Service.GetLanguageInfoAsync("en");
        Assert.That(info.InternalCode, Is.EqualTo("en"));
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IDistributedReaderWriterLockFactory _lockFactory;
        private readonly BuildJobRunnerType _trainJobRunnerType;
        private readonly ClearMLBuildJobRunner _clearMLRunner;
        private readonly ITruecaserFactory _truecaserFactory;
        private readonly ServiceProvider _serviceProvider;
        private readonly IBuildJobService<TranslationEngine>? _deferredBuildJobService;
        private readonly LocalBuildJobRunner _jobRunner;
        private readonly CancellationTokenSource _runnerCts = new();
        private Task? _trainJobTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _training = true;

        public TestEnvironment()
        {
            _trainJobRunnerType = BuildJobRunnerType.ClearML;
            Engines = new MemoryRepository<TranslationEngine>();
            Engines.Add(
                new TranslationEngine
                {
                    Id = EngineId1,
                    EngineId = EngineId1,
                    Type = EngineType.SmtTransfer,
                    SourceLanguage = "es",
                    TargetLanguage = "en",
                    BuildRevision = 1,
                    IsModelPersisted = true,
                }
            );
            TrainSegmentPairs = new MemoryRepository<TrainSegmentPair>();
            PlatformService = Substitute.For<IPlatformService>();
            PlatformService.EngineGroup.Returns(EngineGroup.Translation);
            SmtModel = Substitute.For<IInteractiveTranslationModel>();
            SmtBatchTrainer = Substitute.For<ITrainer>();
            SmtBatchTrainer.Stats.Returns(
                new TrainStats { TrainCorpusSize = 0, Metrics = { { "bleu", 0.0 }, { "perplexity", 0.0 } } }
            );
            Truecaser = Substitute.For<ITruecaser>();
            TruecaserTrainer = Substitute.For<ITrainer>();

            SmtModelFactory = CreateSmtModelFactory();
            TransferEngineFactory = CreateTransferEngineFactory();
            _truecaserFactory = CreateTruecaserFactory();
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
                [new SmtTransferClearMLBuildJobFactory(SharedFileService, Engines)],
                BuildJobOptions
            );

            var smtEngineOptions = Substitute.For<IOptionsMonitor<SmtTransferEngineOptions>>();
            smtEngineOptions.CurrentValue.Returns(new SmtTransferEngineOptions());

            var services = new ServiceCollection();
            services.AddScoped(_ => _deferredBuildJobService!);
            services.AddSingleton(Substitute.For<IBuildJobService<WordAlignmentEngine>>());
            services.AddKeyedSingleton(EngineGroup.Translation, (_, _) => PlatformService);
            services.AddKeyedSingleton(EngineGroup.WordAlignment, (_, _) => Substitute.For<IPlatformService>());
            services.AddSingleton<IRepository<TranslationEngine>>(Engines);
            services.AddSingleton<IRepository<WordAlignmentEngine>>(new MemoryRepository<WordAlignmentEngine>());
            services.AddSingleton<IRepository<TrainSegmentPair>>(TrainSegmentPairs);
            services.AddScoped<IDataAccessContext>(_ => new MemoryDataAccessContext());
            services.AddSingleton(SharedFileService);
            services.AddSingleton(_lockFactory);
            services.AddSingleton(Substitute.For<IParallelCorpusService>());
            services.AddSingleton(BuildJobOptions);
            services.AddSingleton(_truecaserFactory);
            services.AddSingleton(SmtModelFactory);
            services.AddSingleton(TransferEngineFactory);
            services.AddSingleton(smtEngineOptions);
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();

            _jobRunner = CreateJobRunner();
            BuildJobService = CreateBuildJobService();
            _deferredBuildJobService = BuildJobService;
            StateService = CreateStateService();
            Service = CreateService();
            _ = _jobRunner.StartAsync(_runnerCts.Token);
        }

        public SmtTransferEngineService Service { get; private set; }
        public SmtTransferEngineStateService StateService { get; private set; }
        public MemoryRepository<TranslationEngine> Engines { get; }
        public MemoryRepository<TrainSegmentPair> TrainSegmentPairs { get; }
        public ISmtModelFactory SmtModelFactory { get; }
        public ITransferEngineFactory TransferEngineFactory { get; }
        public ITrainer SmtBatchTrainer { get; }
        public IInteractiveTranslationModel SmtModel { get; }
        public ITruecaser Truecaser { get; }
        public ITrainer TruecaserTrainer { get; }
        public IPlatformService PlatformService { get; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }

        public IClearMLService ClearMLService { get; }
        public IClearMLQueueService ClearMLMonitorService { get; }

        public ISharedFileService SharedFileService { get; }

        public IBuildJobService<TranslationEngine> BuildJobService { get; private set; }

        public async Task CommitAsync(TimeSpan inactiveTimeout)
        {
            await StateService.CommitAsync(_lockFactory, Engines, inactiveTimeout);
        }

        public void UseInfiniteTrainJob()
        {
            SmtBatchTrainer.TrainAsync(
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
                [new SmtTransferTestLocalBuildJobFactory(_trainJobRunnerType)],
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _serviceProvider.GetRequiredService<ILogger<LocalBuildJobRunner>>()
            );
        }

        private IBuildJobService<TranslationEngine> CreateBuildJobService()
        {
            return new BuildJobService<TranslationEngine>([_jobRunner, _clearMLRunner], Engines);
        }

        private SmtTransferEngineStateService CreateStateService()
        {
            var options = Substitute.For<IOptionsMonitor<SmtTransferEngineOptions>>();
            options.CurrentValue.Returns(new SmtTransferEngineOptions());
            return new SmtTransferEngineStateService(
                SmtModelFactory,
                TransferEngineFactory,
                _truecaserFactory,
                options,
                Substitute.For<ILogger<SmtTransferEngineStateService>>()
            );
        }

        private SmtTransferEngineService CreateService()
        {
            return new SmtTransferEngineService(
                _lockFactory,
                PlatformService,
                Engines,
                TrainSegmentPairs,
                StateService,
                BuildJobService,
                ClearMLMonitorService
            );
        }

        private ISmtModelFactory CreateSmtModelFactory()
        {
            ISmtModelFactory factory = Substitute.For<ISmtModelFactory>();

            var translationResult = new TranslationResult(
                "this is a TEST.",
                "esto es una prueba .".Split(),
                "this is a TEST .".Split(),
                [1.0, 1.0, 1.0, 1.0, 1.0],
                1.0,
                [
                    TranslationSources.Smt,
                    TranslationSources.Smt,
                    TranslationSources.Smt,
                    TranslationSources.Smt,
                    TranslationSources.Smt,
                ],
                new WordAlignmentMatrix(5, 5)
                {
                    [0, 0] = true,
                    [1, 1] = true,
                    [2, 2] = true,
                    [3, 3] = true,
                    [4, 4] = true,
                },
                [new Phrase(Range<int>.Create(0, 5), 5)]
            );
            SmtModel.Translate(1, Arg.Any<string>()).Returns([translationResult]);
            SmtModel
                .GetWordGraph(Arg.Any<string>())
                .Returns(
                    new WordGraph(
                        "esto es una prueba .".Split(),
                        new[]
                        {
                            new WordGraphArc(
                                0,
                                1,
                                1.0,
                                "this is".Split(),
                                new WordAlignmentMatrix(2, 2) { [0, 0] = true, [1, 1] = true },
                                Range<int>.Create(0, 2),
                                GetSources(2, false),
                                [1.0, 1.0]
                            ),
                            new WordGraphArc(
                                1,
                                2,
                                1.0,
                                "a test".Split(),
                                new WordAlignmentMatrix(2, 2) { [0, 0] = true, [1, 1] = true },
                                Range<int>.Create(2, 4),
                                GetSources(2, false),
                                [1.0, 1.0]
                            ),
                            new WordGraphArc(
                                2,
                                3,
                                1.0,
                                ".".Split(),
                                new WordAlignmentMatrix(1, 1) { [0, 0] = true },
                                Range<int>.Create(4, 5),
                                GetSources(1, false),
                                [1.0]
                            ),
                        },
                        [3]
                    )
                );

            factory
                .Create(
                    Arg.Any<string>(),
                    Arg.Any<IRangeTokenizer<string, int, string>>(),
                    Arg.Any<IDetokenizer<string, string>>(),
                    Arg.Any<ITruecaser>()
                )
                .Returns(SmtModel);
            factory
                .CreateTrainer(
                    Arg.Any<string>(),
                    Arg.Any<IRangeTokenizer<string, int, string>>(),
                    Arg.Any<IParallelTextCorpus>()
                )
                .Returns(SmtBatchTrainer);
            return factory;
        }

        private static ITransferEngineFactory CreateTransferEngineFactory()
        {
            ITransferEngineFactory factory = Substitute.For<ITransferEngineFactory>();
            ITranslationEngine engine = Substitute.For<ITranslationEngine>();
            engine
                .Translate(Arg.Any<string>())
                .Returns(
                    new TranslationResult(
                        "this is a TEST.",
                        "esto es una prueba .".Split(),
                        "this is a TEST .".Split(),
                        [1.0, 1.0, 1.0, 1.0, 1.0],
                        1.0,
                        [
                            TranslationSources.Transfer,
                            TranslationSources.Transfer,
                            TranslationSources.Transfer,
                            TranslationSources.Transfer,
                            TranslationSources.Transfer,
                        ],
                        new WordAlignmentMatrix(5, 5)
                        {
                            [0, 0] = true,
                            [1, 1] = true,
                            [2, 2] = true,
                            [3, 3] = true,
                            [4, 4] = true,
                        },
                        [new Phrase(Range<int>.Create(0, 5), 5)]
                    )
                );
            factory
                .Create(
                    Arg.Any<string>(),
                    Arg.Any<IRangeTokenizer<string, int, string>>(),
                    Arg.Any<IDetokenizer<string, string>>(),
                    Arg.Any<ITruecaser>()
                )
                .Returns(engine);
            return factory;
        }

        private ITruecaserFactory CreateTruecaserFactory()
        {
            ITruecaserFactory factory = Substitute.For<ITruecaserFactory>();
            factory.Create(Arg.Any<string>()).Returns(Truecaser);
            factory
                .CreateTrainer(Arg.Any<string>(), Arg.Any<ITokenizer<string, int, string>>(), Arg.Any<ITextCorpus>())
                .Returns(TruecaserTrainer);
            return factory;
        }

        private static TranslationSources[] GetSources(int count, bool isUnknown)
        {
            var sources = new TranslationSources[count];
            for (int i = 0; i < count; i++)
                sources[i] = isUnknown ? TranslationSources.None : TranslationSources.Smt;
            return sources;
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

                string engineDir = Path.Combine("translation_engines", EngineId1);
                SmtModelFactory.InitNew(engineDir);
                ITextCorpus sourceCorpus = new DictionaryTextCorpus();
                ITextCorpus targetCorpus = new DictionaryTextCorpus();
                IParallelTextCorpus parallelCorpus = sourceCorpus.AlignRows(targetCorpus);
                LatinWordTokenizer tokenizer = new();
                using ITrainer smtModelTrainer = SmtModelFactory.CreateTrainer(engineDir, tokenizer, parallelCorpus);
                using ITrainer truecaseTrainer = _truecaserFactory.CreateTrainer(engineDir, tokenizer, targetCorpus);
                await smtModelTrainer.TrainAsync(null, _cancellationTokenSource.Token);
                await truecaseTrainer.TrainAsync(cancellationToken: _cancellationTokenSource.Token);

                await smtModelTrainer.SaveAsync(_cancellationTokenSource.Token);
                await truecaseTrainer.SaveAsync(_cancellationTokenSource.Token);

                await using Stream engineStream = await SharedFileService.OpenWriteAsync(
                    $"builds/{BuildId1}/model.tar.gz",
                    _cancellationTokenSource.Token
                );

                await using Stream targetStream = await SharedFileService.OpenWriteAsync(
                    $"builds/{BuildId1}/pretranslate.trg.json",
                    _cancellationTokenSource.Token
                );

                await BuildJobService.StartBuildJobAsync(
                    BuildJobRunnerType.Local,
                    EngineType.SmtTransfer,
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

        private class SmtTransferTestLocalBuildJobFactory(BuildJobRunnerType trainJobRunnerType) : ILocalBuildJobFactory
        {
            private static readonly JsonSerializerOptions SerializerOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            public EngineType EngineType => EngineType.SmtTransfer;

            public string? Serialize(BuildStage stage, object? data) =>
                new SmtTransferLocalBuildJobFactory().Serialize(stage, data);

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
                        var preprocessJob = ActivatorUtilities.CreateInstance<SmtTransferPreprocessBuildJob>(
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
                        await new SmtTransferLocalBuildJobFactory().RunAsync(
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
