using Serval.Shared.Contracts;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class PreprocessBuildJobTests
{
    [Test]
    public void RunAsync_NothingToInference()
    {
        TestEnvironment env = new();
        ParallelCorpusContract corpus1 = TestEnvironment.TextFileCorpus(trainOnTextIds: null, inferenceTextIds: []);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await env.RunBuildJobAsync(corpus1);
        });
    }

    [Test]
    public async Task RunAsync_BuildWarnings()
    {
        TestEnvironment env = new();
        ParallelCorpusContract corpus1 = new()
        {
            Id = "corpusId1",
            SourceCorpora =
            [
                new()
                {
                    Id = "src_1",
                    Language = "es",
                    Files = [TestEnvironment.ParatextFile("pt-source1")],
                },
            ],
            TargetCorpora =
            [
                new()
                {
                    Id = "trg_1",
                    Language = "en",
                    Files = [TestEnvironment.ParatextFile("pt-target1")],
                },
            ],
        };
        env.ParallelCorpusService.AnalyzeUsfmVersification(Arg.Any<IEnumerable<ParallelCorpusContract>>())
            .Returns([
                (
                    "corpusId1",
                    "src_1",
                    [
                        new()
                        {
                            ActualVerseRef = "MAT 1:1",
                            ExpectedVerseRef = "MAT 1:1",
                            ProjectName = "pt-source1",
                            Type = Serval.Shared.Contracts.UsfmVersificationErrorType.MissingVerse,
                        },
                        new()
                        {
                            ActualVerseRef = "MAT 1:2",
                            ExpectedVerseRef = "MAT 1:2",
                            ProjectName = "pt-source1",
                            Type = Serval.Shared.Contracts.UsfmVersificationErrorType.ExtraVerse,
                        },
                    ]
                ),
            ]);

        await env.RunBuildJobAsync(corpus1, engineId: "engine4");
        Assert.That(env.ExecutionData.Warnings, Has.Count.EqualTo(2));

        env.BuildJobOptions.CurrentValue.Returns(new BuildJobOptions() { MaxWarnings = 1 });
        await env.RunBuildJobAsync(corpus1, engineId: "engine4");
        // Two warnings after truncation + one warning mentioning that warnings were truncated
        Assert.That(env.ExecutionData.Warnings, Has.Count.EqualTo(2));
    }

    [Test]
    public void RunAsync_UnknownLanguageTagsNoData()
    {
        TestEnvironment env = new();
        ParallelCorpusContract corpus1 = TestEnvironment.TextFileCorpus(sourceLanguage: "xxx", targetLanguage: "zzz");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await env.RunBuildJobAsync(corpus1, engineId: "engine2");
        });
    }

    [Test]
    public async Task RunAsync_UnknownLanguageTagsNoDataSmtTransfer()
    {
        TestEnvironment env = new();
        ParallelCorpusContract corpus1 = TestEnvironment.TextFileCorpus(sourceLanguage: "xxx", targetLanguage: "zzz");

        await env.RunBuildJobAsync(corpus1, engineId: "engine3", engineType: EngineType.SmtTransfer);
    }

    private class TestEnvironment
    {
        public ISharedFileService SharedFileService { get; }
        public IPlatformService PlatformService { get; }
        public MemoryRepository<TranslationEngine> Engines { get; }
        public MemoryRepository<TrainSegmentPair> TrainSegmentPairs { get; }
        public IDistributedReaderWriterLockFactory LockFactory { get; }
        public IBuildJobService<TranslationEngine> BuildJobService { get; }
        public IClearMLService ClearMLService { get; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }
        public IParallelCorpusService ParallelCorpusService { get; }

        public BuildExecutionData ExecutionData { get; private set; } = new BuildExecutionData();

        public TestEnvironment()
        {
            if (!Sldr.IsInitialized)
                Sldr.Initialize(offlineTestMode: true);

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
                    CurrentBuild = new()
                    {
                        BuildId = "build1",
                        JobId = "job1",
                        JobState = BuildJobState.Pending,
                        BuildJobRunner = BuildJobRunnerType.Hangfire,
                        Stage = BuildStage.Preprocess,
                        ExecutionData = new BuildExecutionData(),
                    },
                }
            );
            Engines.Add(
                new TranslationEngine
                {
                    Id = "engine2",
                    EngineId = "engine2",
                    Type = EngineType.Nmt,
                    SourceLanguage = "xxx",
                    TargetLanguage = "zzz",
                    BuildRevision = 1,
                    IsModelPersisted = true,
                    CurrentBuild = new()
                    {
                        BuildId = "build1",
                        JobId = "job1",
                        JobState = BuildJobState.Pending,
                        BuildJobRunner = BuildJobRunnerType.Hangfire,
                        Stage = BuildStage.Preprocess,
                        ExecutionData = new BuildExecutionData(),
                    },
                }
            );
            Engines.Add(
                new TranslationEngine
                {
                    Id = "engine3",
                    EngineId = "engine3",
                    Type = EngineType.SmtTransfer,
                    SourceLanguage = "xxx",
                    TargetLanguage = "zzz",
                    BuildRevision = 1,
                    IsModelPersisted = true,
                    CurrentBuild = new()
                    {
                        BuildId = "build1",
                        JobId = "job1",
                        JobState = BuildJobState.Pending,
                        BuildJobRunner = BuildJobRunnerType.Hangfire,
                        Stage = BuildStage.Preprocess,
                        ExecutionData = new BuildExecutionData(),
                    },
                }
            );
            Engines.Add(
                new TranslationEngine
                {
                    Id = "engine4",
                    EngineId = "engine4",
                    Type = EngineType.Nmt,
                    SourceLanguage = "es",
                    TargetLanguage = "en",
                    BuildRevision = 1,
                    IsModelPersisted = true,
                    CurrentBuild = new()
                    {
                        BuildId = "build1",
                        JobId = "job1",
                        JobState = BuildJobState.Pending,
                        BuildJobRunner = BuildJobRunnerType.Hangfire,
                        Stage = BuildStage.Preprocess,
                        ExecutionData = new BuildExecutionData(),
                    },
                }
            );
            TrainSegmentPairs = new MemoryRepository<TrainSegmentPair>();
            PlatformService = Substitute.For<IPlatformService>();
            PlatformService.EngineGroup.Returns(EngineGroup.Translation);
            PlatformService.UpdateBuildExecutionDataAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Do<BuildExecutionData>(data => ExecutionData = data),
                Arg.Any<CancellationToken>()
            );
            LockFactory = new DistributedReaderWriterLockFactory(
                new OptionsWrapper<ServiceOptions>(new ServiceOptions { ServiceId = "host" }),
                new OptionsWrapper<DistributedReaderWriterLockOptions>(new DistributedReaderWriterLockOptions()),
                new MemoryRepository<RWLock>(),
                new ObjectIdGenerator()
            );
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
                .GetProjectIdAsync("engine2", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("project1"));
            ClearMLService
                .GetProjectIdAsync("engine3", Arg.Any<CancellationToken>())
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
            SharedFileService = new SharedFileService(Substitute.For<ILoggerFactory>());
            BuildJobService = new BuildJobService<TranslationEngine>(
                [
                    new HangfireBuildJobRunner(
                        Substitute.For<IBackgroundJobClient>(),
                        [new NmtHangfireBuildJobFactory(), new SmtTransferHangfireBuildJobFactory()]
                    ),
                    new ClearMLBuildJobRunner(
                        ClearMLService,
                        [
                            new NmtClearMLBuildJobFactory(
                                SharedFileService,
                                Substitute.For<ILanguageTagService>(),
                                Engines
                            ),
                            new SmtTransferClearMLBuildJobFactory(SharedFileService, Engines),
                        ],
                        BuildJobOptions
                    ),
                ],
                Engines
            );
            ParallelCorpusService = Substitute.For<IParallelCorpusService>();
        }

        public PreprocessBuildJob<TranslationEngine> GetBuildJob(EngineType engineType)
        {
            switch (engineType)
            {
                case EngineType.Nmt:
                {
                    return new NmtPreprocessBuildJob(
                        PlatformService,
                        Engines,
                        new MemoryDataAccessContext(),
                        Substitute.For<ILogger<NmtPreprocessBuildJob>>(),
                        BuildJobService,
                        SharedFileService,
                        new LanguageTagService(),
                        ParallelCorpusService,
                        BuildJobOptions
                    );
                }
                case EngineType.SmtTransfer:
                {
                    return new SmtTransferPreprocessBuildJob(
                        PlatformService,
                        Engines,
                        new MemoryDataAccessContext(),
                        Substitute.For<ILogger<SmtTransferPreprocessBuildJob>>(),
                        BuildJobService,
                        SharedFileService,
                        LockFactory,
                        TrainSegmentPairs,
                        ParallelCorpusService,
                        BuildJobOptions
                    );
                }
                default:
                    throw new InvalidOperationException("Unknown engine type.");
            }
        }

        public static ParallelCorpusContract TextFileCorpus(
            HashSet<string>? trainOnTextIds,
            HashSet<string>? inferenceTextIds
        )
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [TextFile("source1")],
                        TrainOnTextIds = trainOnTextIds,
                        InferenceTextIds = inferenceTextIds,
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [TextFile("target1")],
                        TrainOnTextIds = trainOnTextIds,
                    },
                ],
            };
        }

        public static ParallelCorpusContract TextFileCorpus(string sourceLanguage, string targetLanguage)
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "src_1",
                        Language = sourceLanguage,
                        Files = [TextFile("source1")],
                        TrainOnTextIds = [],
                        InferenceTextIds = [],
                    },
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "trg_1",
                        Language = targetLanguage,
                        Files = [TextFile("target1")],
                        TrainOnTextIds = [],
                    },
                ],
            };
        }

        public Task RunBuildJobAsync(
            ParallelCorpusContract corpus,
            bool useKeyTerms = true,
            string engineId = "engine1",
            EngineType engineType = EngineType.Nmt
        )
        {
            return RunBuildJobAsync([corpus], useKeyTerms, engineId, engineType);
        }

        public Task RunBuildJobAsync(
            IEnumerable<ParallelCorpusContract> corpora,
            bool useKeyTerms = true,
            string engineId = "engine1",
            EngineType engineType = EngineType.Nmt
        )
        {
            return GetBuildJob(engineType)
                .RunAsync(
                    engineId,
                    "build1",
                    corpora.ToList(),
                    useKeyTerms ? null : "{\"use_key_terms\":false}",
                    default
                );
        }

        public static CorpusFileContract ParatextFile(string name)
        {
            return new()
            {
                TextId = name,
                Format = FileFormat.Paratext,
                Location = $"{name}.zip",
            };
        }

        private static CorpusFileContract TextFile(string name)
        {
            return new()
            {
                TextId = "textId1",
                Format = FileFormat.Text,
                Location = $"{name}.txt",
            };
        }
    }
}
