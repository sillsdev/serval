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
        await env.Service.CreateAsync(EngineId2, "Engine 2", "es", "en");
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

    // [TestCase(BuildJobRunnerType.Hangfire)] //TODO Implement hangfire?
    [TestCase(BuildJobRunnerType.ClearML)]
    public async Task StartBuildAsync(BuildJobRunnerType trainJobRunnerType)
    {
        using var env = new TestEnvironment(trainJobRunnerType);
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.BuildRevision, Is.EqualTo(1));
        // ensure that the model was loaded before training
        await env.Service.GetBestWordAlignmentAsync(EngineId1, "esto es una prueba.", "this is a test.");
        await env.Service.StartBuildAsync(
            EngineId1,
            BuildId1,
            null,
            [
                new ParallelCorpus()
                {
                    Id = CorpusId1,
                    SourceCorpora = new List<MonolingualCorpus>()
                    {
                        new()
                        {
                            Id = "src",
                            Language = "es",
                            Files = [],
                            TrainOnTextIds = null,
                            InferenceTextIds = null
                        }
                    },
                    TargetCorpora = new List<MonolingualCorpus>()
                    {
                        new()
                        {
                            Id = "trg",
                            Language = "en",
                            Files = [],
                            TrainOnTextIds = null
                        }
                    },
                }
            ]
        );
        await env.WaitForBuildToFinishAsync();
        _ = env.WordAlignmentBatchTrainer.Received()
            .TrainAsync(Arg.Any<IProgress<ProgressStatus>>(), Arg.Any<CancellationToken>());
        _ = env.WordAlignmentBatchTrainer.Received().SaveAsync(Arg.Any<CancellationToken>());
        engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Null);
        Assert.That(engine.BuildRevision, Is.EqualTo(2));
        // check if model was reloaded upon first use after training
        env.WordAlignmentModel.ClearReceivedCalls();
        await env.Service.GetBestWordAlignmentAsync(EngineId1, "esto es una prueba.", "this is a test.");
        env.WordAlignmentModel.Received().Dispose();
    }

    // [TestCase(BuildJobRunnerType.Hangfire)] //TODO implement Hangfire?
    [TestCase(BuildJobRunnerType.ClearML)]
    public async Task CancelBuildAsync_Building(BuildJobRunnerType trainJobRunnerType)
    {
        using var env = new TestEnvironment(trainJobRunnerType);
        env.UseInfiniteTrainJob();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, "{}", Array.Empty<ParallelCorpus>());
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

    // [TestCase(BuildJobRunnerType.Hangfire)] //TODO implement Hangfire?
    [TestCase(BuildJobRunnerType.ClearML)]
    public void CancelBuildAsync_NotBuilding(BuildJobRunnerType trainJobRunnerType)
    {
        using var env = new TestEnvironment(trainJobRunnerType);
        Assert.ThrowsAsync<InvalidOperationException>(() => env.Service.CancelBuildAsync(EngineId1));
    }

    // [TestCase(BuildJobRunnerType.Hangfire)] //TODO implement Hangfire?
    [TestCase(BuildJobRunnerType.ClearML)]
    public async Task DeleteAsync_WhileBuilding(BuildJobRunnerType trainJobRunnerType)
    {
        using var env = new TestEnvironment(trainJobRunnerType);
        env.UseInfiniteTrainJob();

        await env.Service.StartBuildAsync(EngineId1, BuildId1, "{}", Array.Empty<ParallelCorpus>());
        await env.WaitForTrainingToStartAsync();
        WordAlignmentEngine engine = env.Engines.Get(EngineId1);
        Assert.That(engine.CurrentBuild, Is.Not.Null);
        Assert.That(engine.CurrentBuild!.JobState, Is.EqualTo(BuildJobState.Active));
        await env.Service.DeleteAsync(EngineId1);
        await env.WaitForBuildToFinishAsync();
        await env.WaitForAllHangfireJobsToFinishAsync();
        _ = env.WordAlignmentBatchTrainer.DidNotReceive().SaveAsync();
        Assert.That(env.Engines.Contains(EngineId1), Is.False);
    }

    [Test]
    public async Task GetBestWordAlignment()
    {
        using var env = new TestEnvironment();
        WordAlignmentResult result = await env.Service.GetBestWordAlignmentAsync(
            EngineId1,
            "esto es una prueba.",
            "this is a test."
        );
        Assert.That(string.Join(' ', result.TargetTokens), Is.EqualTo("this is a test ."));
        Assert.That(result.Confidences, Has.Count.EqualTo(5));
        Assert.That(result.Alignment[0, 0], Is.True);
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly Hangfire.InMemory.InMemoryStorage _memoryStorage;
        private readonly BackgroundJobClient _jobClient;
        private BackgroundJobServer _jobServer;
        private readonly IDistributedReaderWriterLockFactory _lockFactory;
        private readonly BuildJobRunnerType _trainJobRunnerType;
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
            _memoryStorage = new Hangfire.InMemory.InMemoryStorage();
            _jobClient = new BackgroundJobClient(_memoryStorage);
            PlatformService = Substitute.For<IPlatformService>();
            PlatformService.EngineGroup.Returns(EngineGroup.WordAlignment);
            WordAlignmentModel = Substitute.For<IWordAlignmentModel>();
            WordAlignmentBatchTrainer = Substitute.For<ITrainer>();
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
            var buildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
            buildJobOptions.CurrentValue.Returns(
                new BuildJobOptions
                {
                    ClearML =
                    [
                        new ClearMLBuildQueue()
                        {
                            EngineType = EngineType.Statistical.ToString().ToString(),
                            ModelType = "thot",
                            DockerImage = "default",
                            Queue = "default"
                        }
                    ]
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
                buildJobOptions,
                Substitute.For<ILogger<ClearMLMonitorService>>()
            );
            BuildJobService = new BuildJobService<WordAlignmentEngine>(
                [
                    new HangfireBuildJobRunner(_jobClient, [new StatisticalHangfireBuildJobFactory()]),
                    new ClearMLBuildJobRunner(
                        ClearMLService,
                        [new StatisticalClearMLBuildJobFactory(SharedFileService, Engines)],
                        buildJobOptions
                    )
                ],
                Engines
            );
            _jobServer = CreateJobServer();
            StateService = CreateStateService();
            Service = CreateService();
        }

        public StatisticalEngineService Service { get; private set; }
        public WordAlignmentEngineStateService StateService { get; private set; }
        public MemoryRepository<WordAlignmentEngine> Engines { get; }
        public IWordAlignmentModelFactory WordAlignmentModelFactory { get; }
        public ITrainer WordAlignmentBatchTrainer { get; }
        public IWordAlignmentModel WordAlignmentModel { get; }
        public IPlatformService PlatformService { get; }

        public IClearMLService ClearMLService { get; }
        public IClearMLQueueService ClearMLMonitorService { get; }

        public ISharedFileService SharedFileService { get; }

        public IBuildJobService<WordAlignmentEngine> BuildJobService { get; }

        public async Task CommitAsync(TimeSpan inactiveTimeout)
        {
            await StateService.CommitAsync(_lockFactory, Engines, inactiveTimeout);
        }

        public void StopServer()
        {
            _jobServer.Dispose();
            StateService.Dispose();
        }

        public void StartServer()
        {
            _jobServer = CreateJobServer();
            StateService = CreateStateService();
            Service = CreateService();
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

        private BackgroundJobServer CreateJobServer()
        {
            var jobServerOptions = new BackgroundJobServerOptions
            {
                Activator = new EnvActivator(this),
                Queues = new[] { "statistical" },
                CancellationCheckInterval = TimeSpan.FromMilliseconds(50),
            };
            return new BackgroundJobServer(jobServerOptions, _memoryStorage);
        }

        private WordAlignmentEngineStateService CreateStateService()
        {
            var options = Substitute.For<IOptionsMonitor<WordAlignmentEngineOptions>>();
            options.CurrentValue.Returns(new WordAlignmentEngineOptions());
            return new WordAlignmentEngineStateService(
                WordAlignmentModelFactory,
                options,
                Substitute.For<ILogger<WordAlignmentEngineStateService>>()
            );
        }

        private StatisticalEngineService CreateService()
        {
            return new StatisticalEngineService(
                _lockFactory,
                new[] { PlatformService },
                new MemoryDataAccessContext(),
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

        public async Task WaitForAllHangfireJobsToFinishAsync()
        {
            IMonitoringApi monitoringApi = _memoryStorage.GetMonitoringApi();
            while (monitoringApi.EnqueuedCount("statistical") > 0 || monitoringApi.ProcessingCount() > 0)
                await Task.Delay(50);
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
            _jobServer.Dispose();
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
                    BuildJobRunnerType.Hangfire,
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

        private class EnvActivator(TestEnvironment env) : JobActivator
        {
            private readonly TestEnvironment _env = env;

            public override object ActivateJob(Type jobType)
            {
                if (jobType == typeof(WordAlignmentPreprocessBuildJob))
                {
                    return new WordAlignmentPreprocessBuildJob(
                        new[] { _env.PlatformService },
                        _env.Engines,
                        new MemoryDataAccessContext(),
                        Substitute.For<ILogger<WordAlignmentPreprocessBuildJob>>(),
                        _env.BuildJobService,
                        _env.SharedFileService,
                        new ParallelCorpusPreprocessingService(new CorpusService())
                    )
                    {
                        TrainJobRunnerType = _env._trainJobRunnerType
                    };
                }
                if (jobType == typeof(StatisticalPostprocessBuildJob))
                {
                    var engineOptions = Substitute.For<IOptionsMonitor<WordAlignmentEngineOptions>>();
                    engineOptions.CurrentValue.Returns(new WordAlignmentEngineOptions());
                    var buildJobOptions = Substitute.For<IOptionsMonitor<BuildJobOptions>>();
                    buildJobOptions.CurrentValue.Returns(new BuildJobOptions());
                    return new StatisticalPostprocessBuildJob(
                        new[] { _env.PlatformService },
                        _env.Engines,
                        new MemoryDataAccessContext(),
                        _env.BuildJobService,
                        Substitute.For<ILogger<StatisticalPostprocessBuildJob>>(),
                        _env.SharedFileService,
                        _env._lockFactory,
                        _env.WordAlignmentModelFactory,
                        buildJobOptions,
                        engineOptions
                    );
                }
                if (jobType == typeof(StatisticalTrainBuildJob))
                {
                    return new StatisticalTrainBuildJob(
                        new[] { _env.PlatformService },
                        _env.Engines,
                        new MemoryDataAccessContext(),
                        _env.BuildJobService,
                        Substitute.For<ILogger<StatisticalTrainBuildJob>>()
                    );
                }
                return base.ActivateJob(jobType);
            }
        }
    }
}
