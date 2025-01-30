namespace Serval.Machine.Shared.Services;

[TestFixture]
public class PreprocessBuildJobTests
{
    [Test]
    public async Task RunAsync_FilterOutEverything()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.DefaultTextFileCorpus with { };

        await env.RunBuildJobAsync(corpus1);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(0));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(0));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RunAsync_TrainOnAll()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(trainOnTextIds: null, inferenceTextIds: []);

        await env.RunBuildJobAsync(corpus1);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(4));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RunAsync_TrainOnTextIds()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(trainOnTextIds: ["textId1"], inferenceTextIds: []);

        await env.RunBuildJobAsync(corpus1);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(4));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RunAsync_TrainAndPretranslateAll()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(trainOnTextIds: null, inferenceTextIds: null);

        await env.RunBuildJobAsync(corpus1);

        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_PretranslateAll()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(trainOnTextIds: [], inferenceTextIds: null);

        await env.RunBuildJobAsync(corpus1);

        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(4));
    }

    [Test]
    public async Task RunAsync_InferenceTextIds()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(inferenceTextIds: ["textId1"], trainOnTextIds: null);

        await env.RunBuildJobAsync(corpus1);

        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_InferenceTextIdsOverlapWithTrainOnTextIds()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(
            inferenceTextIds: ["textId1"],
            trainOnTextIds: ["textId1"]
        );

        await env.RunBuildJobAsync(corpus1);
        Assert.Multiple(async () =>
        {
            Assert.That((await env.GetTrainCountAsync()).Source1Count, Is.EqualTo(4));
            Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task RunAsync_EnableKeyTerms()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.DefaultParatextCorpus;

        await env.RunBuildJobAsync(corpus1, useKeyTerms: true);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(14));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(3416));
        });
    }

    [Test]
    public async Task RunAsync_EnableKeyTermsNoTrainingData()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.DefaultParatextCorpus;
        corpus1.SourceCorpora[0].TrainOnTextIds = new HashSet<string>();
        corpus1.TargetCorpora[0].TrainOnTextIds = new HashSet<string>();

        await env.RunBuildJobAsync(corpus1, useKeyTerms: true);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(0));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(0));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RunAsync_DisableKeyTerms()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.DefaultParatextCorpus;

        await env.RunBuildJobAsync(corpus1, useKeyTerms: false);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(14));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RunAsync_InferenceChapters()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.ParatextCorpus(
            trainOnChapters: [],
            inferenceChapters: new Dictionary<string, HashSet<int>>
            {
                {
                    "1CH",
                    new HashSet<int> { 12 }
                }
            }
        );

        await env.RunBuildJobAsync(corpus1);

        Assert.That(
            await env.GetPretranslateCountAsync(),
            Is.EqualTo(4),
            JsonSerializer.Serialize(await env.GetPretranslationsAsync())
        );
    }

    [Test]
    public async Task RunAsync_TrainOnChapters()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.ParatextCorpus(
            trainOnChapters: new Dictionary<string, HashSet<int>>
            {
                {
                    "MAT",
                    new HashSet<int> { 1 }
                }
            },
            inferenceChapters: []
        );

        await env.RunBuildJobAsync(corpus1, useKeyTerms: false);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(5));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(0));
            Assert.That(termCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RunAsync_MixedSource_Paratext()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.DefaultMixedSourceParatextCorpus;

        await env.RunBuildJobAsync(corpus1, useKeyTerms: false);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(7));
            Assert.That(src2Count, Is.EqualTo(13));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(15));
    }

    [Test]
    public async Task RunAsync_MixedSource_Text()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.DefaultMixedSourceTextFileCorpus;

        await env.RunBuildJobAsync(corpus1);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(1));
            Assert.That(src2Count, Is.EqualTo(4));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(3));
    }

    [Test]
    public void RunAsync_UnknownLanguageTagsNoData()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(sourceLanguage: "xxx", targetLanguage: "zzz");

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await env.RunBuildJobAsync(corpus1, engineId: "engine2");
        });
    }

    [Test]
    public async Task RunAsync_UnknownLanguageTagsNoDataSmtTransfer()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = TestEnvironment.TextFileCorpus(sourceLanguage: "xxx", targetLanguage: "zzz");

        await env.RunBuildJobAsync(corpus1, engineId: "engine2", engineType: EngineType.SmtTransfer);
    }

    [Test]
    public async Task RunAsync_RemoveFreestandingEllipses()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus1 = env.ParatextCorpus(
            trainOnChapters: new Dictionary<string, HashSet<int>>
            {
                {
                    "MAT",
                    new HashSet<int>() { 2 }
                }
            },
            inferenceChapters: new Dictionary<string, HashSet<int>>
            {
                {
                    "MAT",
                    new HashSet<int>() { 2 }
                }
            }
        );
        await env.RunBuildJobAsync(corpus1, useKeyTerms: false);
        string sourceExtract = await env.GetSourceExtractAsync();
        Assert.That(
            sourceExtract,
            Is.EqualTo("Source one, chapter two, verse one.\nSource one, chapter two, verse two.\n\n"),
            sourceExtract
        );
        string targetExtract = await env.GetTargetExtractAsync();
        Assert.That(
            targetExtract,
            Is.EqualTo("Target one, chapter two, verse one.\n\nTarget one, chapter two, verse three.\n"),
            targetExtract
        );
        JsonArray? pretranslations = await env.GetPretranslationsAsync();
        Assert.That(pretranslations, Is.Not.Null);
        Assert.That(pretranslations!.Count, Is.EqualTo(1));
    }

    [Test]
    public void RunAsync_OnlyParseSelectedBooks_NoBadBooks()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus = env.ParatextCorpus(trainOnTextIds: new() { "LEV" }, inferenceTextIds: new() { "MRK" });

        env.CorpusService = Substitute.For<ICorpusService>();
        env.CorpusService.CreateTextCorpora(Arg.Any<IReadOnlyList<CorpusFile>>())
            .Returns(
                new List<ITextCorpus>()
                {
                    new DummyCorpus(new List<string>() { "LEV", "MRK", "MAT" }, new List<string>() { "MAT" })
                }
            );
        Assert.DoesNotThrowAsync(async () =>
        {
            await env.RunBuildJobAsync(corpus);
        });
    }

    [Test]
    public void RunAsync_OnlyParseSelectedBooks_TrainOnBadBook()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus = env.ParatextCorpus(trainOnTextIds: new() { "MAT" }, inferenceTextIds: new() { "MRK" });
        env.CorpusService = Substitute.For<ICorpusService>();
        env.CorpusService.CreateTextCorpora(Arg.Any<IReadOnlyList<CorpusFile>>())
            .Returns(
                new List<ITextCorpus>()
                {
                    new DummyCorpus(new List<string>() { "LEV", "MRK", "MAT" }, new List<string>() { "MAT" })
                }
            );
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await env.RunBuildJobAsync(corpus);
        });
    }

    [Test]
    public void RunAsync_OnlyParseSelectedBooks_PretranslateOnBadBook()
    {
        using TestEnvironment env = new();
        ParallelCorpus corpus = env.ParatextCorpus(trainOnTextIds: new() { "LEV" }, inferenceTextIds: new() { "MAT" });
        env.CorpusService = Substitute.For<ICorpusService>();
        env.CorpusService.CreateTextCorpora(Arg.Any<IReadOnlyList<CorpusFile>>())
            .Returns(
                new List<ITextCorpus>()
                {
                    new DummyCorpus(new List<string>() { "LEV", "MRK", "MAT" }, new List<string>() { "MAT" })
                }
            );
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await env.RunBuildJobAsync(corpus);
        });
    }

    [Test]
    public async Task ParallelCorpusAsync()
    {
        using TestEnvironment env = new();
        var corpora = new List<ParallelCorpus>()
        {
            new ParallelCorpus()
            {
                Id = "1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "_1",
                        Language = "en",
                        Files = new List<CorpusFile> { env.ParatextFile("pt-source1") },
                        TrainOnChapters = new()
                        {
                            {
                                "MAT",
                                new() { 1 }
                            },
                            {
                                "LEV",
                                new() { }
                            }
                        },
                        InferenceChapters = new()
                        {
                            {
                                "1CH",
                                new() { }
                            }
                        }
                    },
                    new()
                    {
                        Id = "_1",
                        Language = "en",
                        Files = new List<CorpusFile> { env.ParatextFile("pt-source2") },
                        TrainOnChapters = new()
                        {
                            {
                                "MAT",
                                new() { 1 }
                            },
                            {
                                "MRK",
                                new() { }
                            }
                        },
                        InferenceChapters = new()
                        {
                            {
                                "1CH",
                                new() { }
                            }
                        }
                    },
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "_1",
                        Language = "en",
                        Files = new List<CorpusFile> { env.ParatextFile("pt-target1") },
                        TrainOnChapters = new()
                        {
                            {
                                "MAT",
                                new() { 1 }
                            },
                            {
                                "MRK",
                                new() { }
                            }
                        }
                    },
                    new()
                    {
                        Id = "_2",
                        Language = "en",
                        Files = new List<CorpusFile> { env.ParatextFile("pt-target2") },
                        TrainOnChapters = new()
                        {
                            {
                                "MAT",
                                new() { 1 }
                            },
                            {
                                "MRK",
                                new() { }
                            },
                            {
                                "LEV",
                                new() { }
                            }
                        }
                    }
                }
            }
        };
        await env.RunBuildJobAsync(corpora, useKeyTerms: false);
        JsonArray? pretranslations = await env.GetPretranslationsAsync();
        Assert.Multiple(async () =>
        {
            string src = await env.GetSourceExtractAsync();
            Assert.That(
                src,
                Is.EqualTo(
                    @"Source one, chapter fourteen, verse fifty-five. Segment b.
Source one, chapter fourteen, verse fifty-six.
Source two, chapter one, verse one.
Source two, chapter one, verse two.
Source two, chapter one, verse three.
Source one, chapter one, verse four.
Source two, chapter one, verse five. Source two, chapter one, verse six.
Source one, chapter one, verse seven, eight, and nine. Source one, chapter one, verse ten.
Source two, chapter one, verse one.
"
                ),
                src
            );
            string trg = await env.GetTargetExtractAsync();
            Assert.That(
                trg,
                Is.EqualTo(
                    @"Target two, chapter fourteen, verse fifty-five.
Target two, chapter fourteen, verse fifty-six.
Target one, chapter one, verse one.
Target one, chapter one, verse two.
Target one, chapter one, verse three.

Target one, chapter one, verse five and six.
Target one, chapter one, verse seven and eight. Target one, chapter one, verse nine and ten.

"
                ),
                trg
            );
            Assert.That(pretranslations, Is.Not.Null);
            Assert.That(pretranslations!.Count, Is.EqualTo(7));
            Assert.That(
                pretranslations[2]!["translation"]!.ToString(),
                Is.EqualTo("Source one, chapter twelve, verse one.")
            );
        });
    }

    private class TestEnvironment : DisposableBase
    {
        private static readonly string TestDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "Services",
            "data"
        );

        private readonly TempDirectory _tempDir;

        public ISharedFileService SharedFileService { get; }
        public ICorpusService CorpusService { get; set; }
        public IPlatformService PlatformService { get; }
        public MemoryRepository<TranslationEngine> Engines { get; }
        public MemoryRepository<TrainSegmentPair> TrainSegmentPairs { get; }
        public IDistributedReaderWriterLockFactory LockFactory { get; }
        public IBuildJobService<TranslationEngine> BuildJobService { get; }
        public IClearMLService ClearMLService { get; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }

        public ParallelCorpus DefaultTextFileCorpus { get; }
        public ParallelCorpus DefaultMixedSourceTextFileCorpus { get; }
        public ParallelCorpus DefaultParatextCorpus { get; }
        public ParallelCorpus DefaultMixedSourceParatextCorpus { get; }

        public TestEnvironment()
        {
            if (!Sldr.IsInitialized)
                Sldr.Initialize(offlineMode: true);

            _tempDir = new TempDirectory("PreprocessBuildJobTests");

            ZipParatextProject("pt-source1");
            ZipParatextProject("pt-source2");
            ZipParatextProject("pt-target1");
            ZipParatextProject("pt-target2");

            DefaultTextFileCorpus = new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [TextFile("source1")],
                        TrainOnTextIds = [],
                        InferenceTextIds = []
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [TextFile("target1")],
                        TrainOnTextIds = []
                    }
                }
            };

            DefaultMixedSourceTextFileCorpus = new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [TextFile("source1"), TextFile("source2")],
                        TrainOnTextIds = null,
                        TrainOnChapters = null,
                        InferenceTextIds = null,
                        InferenceChapters = null,
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [TextFile("target1")],
                        TrainOnChapters = null,
                        TrainOnTextIds = null
                    }
                }
            };

            DefaultParatextCorpus = new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source1")],
                        TrainOnTextIds = null,
                        InferenceTextIds = null
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [ParatextFile("pt-target1")],
                        TrainOnTextIds = null
                    }
                }
            };

            DefaultMixedSourceParatextCorpus = new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source1")],
                        TrainOnTextIds = null,
                        InferenceTextIds = null
                    },
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source2")],
                        TrainOnTextIds = null,
                        InferenceTextIds = null
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [ParatextFile("pt-target1")],
                        TrainOnTextIds = null
                    }
                }
            };

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
                        Stage = BuildStage.Preprocess
                    }
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
                    IsModelPersisted = false,
                    CurrentBuild = new()
                    {
                        BuildId = "build1",
                        JobId = "job1",
                        JobState = BuildJobState.Pending,
                        BuildJobRunner = BuildJobRunnerType.Hangfire,
                        Stage = BuildStage.Preprocess
                    }
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
                    IsModelPersisted = false,
                    CurrentBuild = new()
                    {
                        BuildId = "build1",
                        JobId = "job1",
                        JobState = BuildJobState.Pending,
                        BuildJobRunner = BuildJobRunnerType.Hangfire,
                        Stage = BuildStage.Preprocess
                    }
                }
            );
            TrainSegmentPairs = new MemoryRepository<TrainSegmentPair>();
            CorpusService = new CorpusService();
            PlatformService = Substitute.For<IPlatformService>();
            PlatformService.EngineGroup.Returns(EngineGroup.Translation);
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
                            EngineType = EngineType.Nmt.ToString(),
                            ModelType = "huggingface",
                            DockerImage = "default",
                            Queue = "default"
                        },
                        new ClearMLBuildQueue()
                        {
                            EngineType = EngineType.SmtTransfer.ToString(),
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
                .GetProjectIdAsync("engine2", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("project1"));
            ClearMLService
                .GetProjectIdAsync("engine2", Arg.Any<CancellationToken>())
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
                        [new NmtHangfireBuildJobFactory()]
                    ),
                    new ClearMLBuildJobRunner(
                        ClearMLService,
                        [
                            new NmtClearMLBuildJobFactory(
                                SharedFileService,
                                Substitute.For<ILanguageTagService>(),
                                Engines
                            )
                        ],
                        BuildJobOptions
                    )
                ],
                Engines
            );
        }

        public PreprocessBuildJob<TranslationEngine> GetBuildJob(EngineType engineType)
        {
            switch (engineType)
            {
                case EngineType.Nmt:
                {
                    return new NmtPreprocessBuildJob(
                        new[] { PlatformService },
                        Engines,
                        new MemoryDataAccessContext(),
                        Substitute.For<ILogger<NmtPreprocessBuildJob>>(),
                        BuildJobService,
                        SharedFileService,
                        new LanguageTagService(),
                        new ParallelCorpusPreprocessingService(CorpusService)
                    );
                }
                case EngineType.SmtTransfer:
                {
                    return new SmtTransferPreprocessBuildJob(
                        new[] { PlatformService },
                        Engines,
                        new MemoryDataAccessContext(),
                        Substitute.For<ILogger<SmtTransferPreprocessBuildJob>>(),
                        BuildJobService,
                        SharedFileService,
                        LockFactory,
                        TrainSegmentPairs,
                        new ParallelCorpusPreprocessingService(CorpusService)
                    );
                }
                default:
                    throw new InvalidOperationException("Unknown engine type.");
            }
            ;
        }

        public static ParallelCorpus TextFileCorpus(HashSet<string>? trainOnTextIds, HashSet<string>? inferenceTextIds)
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [TextFile("source1")],
                        TrainOnTextIds = trainOnTextIds,
                        InferenceTextIds = inferenceTextIds
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [TextFile("target1")],
                        TrainOnTextIds = trainOnTextIds
                    }
                }
            };
        }

        public static ParallelCorpus TextFileCorpus(string sourceLanguage, string targetLanguage)
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = sourceLanguage,
                        Files = [TextFile("source1")],
                        TrainOnTextIds = [],
                        InferenceTextIds = []
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = targetLanguage,
                        Files = [TextFile("target1")],
                        TrainOnTextIds = []
                    }
                }
            };
        }

        public ParallelCorpus ParatextCorpus(
            Dictionary<string, HashSet<int>>? trainOnChapters,
            Dictionary<string, HashSet<int>>? inferenceChapters
        )
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source1")],
                        TrainOnChapters = trainOnChapters,
                        InferenceChapters = inferenceChapters
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [ParatextFile("pt-target1")],
                        TrainOnChapters = trainOnChapters
                    }
                }
            };
        }

        public ParallelCorpus ParatextCorpus(HashSet<string>? trainOnTextIds, HashSet<string>? inferenceTextIds)
        {
            return new()
            {
                Id = "corpusId1",
                SourceCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "src_1",
                        Language = "es",
                        Files = [ParatextFile("pt-source1")],
                        TrainOnTextIds = trainOnTextIds,
                        InferenceTextIds = inferenceTextIds
                    }
                },
                TargetCorpora = new List<MonolingualCorpus>()
                {
                    new()
                    {
                        Id = "trg_1",
                        Language = "en",
                        Files = [ParatextFile("pt-target1")],
                        TrainOnTextIds = trainOnTextIds
                    }
                }
            };
        }

        public Task RunBuildJobAsync(
            ParallelCorpus corpus,
            bool useKeyTerms = true,
            string engineId = "engine1",
            EngineType engineType = EngineType.Nmt
        )
        {
            return RunBuildJobAsync([corpus], useKeyTerms, engineId, engineType);
        }

        public Task RunBuildJobAsync(
            IEnumerable<ParallelCorpus> corpora,
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

        public async Task<string> GetSourceExtractAsync()
        {
            using StreamReader srcReader = new(await SharedFileService.OpenReadAsync("builds/build1/train.src.txt"));
            return await srcReader.ReadToEndAsync();
        }

        public async Task<string> GetTargetExtractAsync()
        {
            using StreamReader trgReader = new(await SharedFileService.OpenReadAsync("builds/build1/train.trg.txt"));
            return await trgReader.ReadToEndAsync();
        }

        public async Task<(int Source1Count, int Source2Count, int TargetCount, int TermCount)> GetTrainCountAsync()
        {
            using StreamReader srcReader = new(await SharedFileService.OpenReadAsync("builds/build1/train.src.txt"));
            using StreamReader trgReader = new(await SharedFileService.OpenReadAsync("builds/build1/train.trg.txt"));
            int src1Count = 0;
            int src2Count = 0;
            int trgCount = 0;
            int termCount = 0;
            string? srcLine;
            string? trgLine;
            while (
                (srcLine = await srcReader.ReadLineAsync()) is not null
                && (trgLine = await trgReader.ReadLineAsync()) is not null
            )
            {
                srcLine = srcLine.Trim();
                trgLine = trgLine.Trim();
                if (srcLine.StartsWith("Source one"))
                    src1Count++;
                else if (srcLine.StartsWith("Source two"))
                    src2Count++;
                else if (srcLine.Length == 0)
                    trgCount++;
                else
                    termCount++;
            }
            return (src1Count, src2Count, trgCount, termCount);
        }

        public async Task<JsonArray?> GetPretranslationsAsync()
        {
            using StreamReader reader =
                new(await SharedFileService.OpenReadAsync("builds/build1/pretranslate.src.json"));
            return JsonSerializer.Deserialize<JsonArray>(await reader.ReadToEndAsync());
        }

        public async Task<int> GetPretranslateCountAsync()
        {
            var pretranslations = await GetPretranslationsAsync();
            return pretranslations?.Count ?? 0;
        }

        private void ZipParatextProject(string name)
        {
            ZipFile.CreateFromDirectory(Path.Combine(TestDataPath, name), Path.Combine(_tempDir.Path, $"{name}.zip"));
        }

        public CorpusFile ParatextFile(string name)
        {
            return new()
            {
                TextId = name,
                Format = FileFormat.Paratext,
                Location = Path.Combine(_tempDir.Path, $"{name}.zip")
            };
        }

        private static CorpusFile TextFile(string name)
        {
            return new()
            {
                TextId = "textId1",
                Format = FileFormat.Text,
                Location = Path.Combine(TestDataPath, $"{name}.txt")
            };
        }

        protected override void DisposeManagedResources()
        {
            _tempDir.Dispose();
        }
    }

    private class DummyCorpus(IEnumerable<string> books, IEnumerable<string> failsOn) : ITextCorpus
    {
        private IEnumerable<string> FailsOn { get; } = failsOn;

        public IEnumerable<IText> Texts =>
            books.Select(b => new MemoryText(
                b,
                new List<TextRow>() { new TextRow(b, new ScriptureRef(new VerseRef("MAT", "1", "1", ScrVers.English))) }
            ));

        public bool IsTokenized => false;

        public ScrVers Versification => ScrVers.English;

        public int Count(bool includeEmpty = true, IEnumerable<string>? textIds = null)
        {
            throw new NotImplementedException();
        }

        public int Count(bool includeEmpty = true)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<TextRow> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TextRow> GetRows(IEnumerable<string> textIds)
        {
            if (textIds.Intersect(FailsOn).Any())
            {
                throw new ArgumentException(
                    $"Text ids provided ({string.Join(',', textIds)}) include text ids specified to fail on ({string.Join(',', FailsOn)})."
                );
            }
            return Texts.Where(t => textIds.Contains(t.Id)).SelectMany(t => t.GetRows());
        }

        public IEnumerable<TextRow> GetRows()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Texts.GetEnumerator();
        }
    }
}
