namespace Serval.Machine.Shared.Services;

[TestFixture]
public class PreprocessBuildJobTests
{
    [Test]
    public async Task RunAsync_FilterOutEverything()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultTextFileCorpus with { };

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
        Corpus corpus1 = env.DefaultTextFileCorpus with { TrainOnTextIds = null };

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
        Corpus corpus1 = env.DefaultTextFileCorpus with { TrainOnTextIds = ["textId1"] };

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
        Corpus corpus1 = env.DefaultTextFileCorpus with { PretranslateTextIds = null, TrainOnTextIds = null };

        await env.RunBuildJobAsync(corpus1);

        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_PretranslateAll()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultTextFileCorpus with { PretranslateTextIds = null };

        await env.RunBuildJobAsync(corpus1);

        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(4));
    }

    [Test]
    public async Task RunAsync_PretranslateTextIds()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultTextFileCorpus with { PretranslateTextIds = ["textId1"], TrainOnTextIds = null };

        await env.RunBuildJobAsync(corpus1);

        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_EnableKeyTerms()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultParatextCorpus with { };

        await env.RunBuildJobAsync(corpus1, useKeyTerms: true);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(0));
            Assert.That(src2Count, Is.EqualTo(0));
            Assert.That(trgCount, Is.EqualTo(0));
            Assert.That(termCount, Is.EqualTo(5726));
        });
    }

    [Test]
    public async Task RunAsync_DisableKeyTerms()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultParatextCorpus with { };

        await env.RunBuildJobAsync(corpus1, useKeyTerms: false);

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
    public async Task RunAsync_PretranslateChapters()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultParatextCorpus with
        {
            PretranslateChapters = new Dictionary<string, HashSet<int>>
            {
                {
                    "1CH",
                    new HashSet<int> { 12 }
                }
            }
        };

        await env.RunBuildJobAsync(corpus1);

        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(4));
    }

    [Test]
    public async Task RunAsync_TrainOnChapters()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultParatextCorpus with
        {
            TrainOnChapters = new Dictionary<string, HashSet<int>>
            {
                {
                    "MAT",
                    new HashSet<int> { 1 }
                }
            }
        };

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
        Corpus corpus1 = env.DefaultMixedSourceParatextCorpus with
        {
            TrainOnTextIds = null,
            PretranslateTextIds = null
        };

        await env.RunBuildJobAsync(corpus1, useKeyTerms: false);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(5));
            Assert.That(src2Count, Is.EqualTo(12));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(13));
    }

    [Test]
    public async Task RunAsync_MixedSource_Text()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultMixedSourceTextFileCorpus with
        {
            TrainOnTextIds = null,
            PretranslateTextIds = null,
            TrainOnChapters = null,
            PretranslateChapters = null
        };

        await env.RunBuildJobAsync(corpus1);

        (int src1Count, int src2Count, int trgCount, int termCount) = await env.GetTrainCountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(src1Count, Is.EqualTo(3));
            Assert.That(src2Count, Is.EqualTo(2));
            Assert.That(trgCount, Is.EqualTo(1));
            Assert.That(termCount, Is.EqualTo(0));
        });
        Assert.That(await env.GetPretranslateCountAsync(), Is.EqualTo(2));
    }

    [Test]
    public void RunAsync_UnknownLanguageTagsNoData()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultTextFileCorpus with { SourceLanguage = "xxx", TargetLanguage = "zzz" };

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await env.RunBuildJobAsync(corpus1, engineId: "engine2");
        });
    }

    [Test]
    public async Task RunAsync_UnknownLanguageTagsNoDataSmtTransfer()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultTextFileCorpus with { SourceLanguage = "xxx", TargetLanguage = "zzz" };

        await env.RunBuildJobAsync(corpus1, engineId: "engine2", engineType: TranslationEngineType.SmtTransfer);
    }

    [Test]
    public async Task RunAsync_RemoveFreestandingEllipses()
    {
        using TestEnvironment env = new();
        Corpus corpus1 = env.DefaultParatextCorpus with
        {
            TrainOnChapters = new Dictionary<string, HashSet<int>>
            {
                {
                    "MAT",
                    new HashSet<int>() { 2 }
                }
            },
            PretranslateChapters = new Dictionary<string, HashSet<int>>
            {
                {
                    "MAT",
                    new HashSet<int>() { 2 }
                }
            }
        };
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
        Assert.That(pretranslations.Count, Is.EqualTo(1));
        Assert.That(pretranslations[0]!["translation"]!.ToString(), Is.EqualTo("Source one, chapter two, verse two."));
    }

    [Test]
    public void RunAsync_OnlyParseSelectedBooks_NoBadBooks()
    {
        using TestEnvironment env = new();
        Corpus corpus = env.DefaultParatextCorpus with
        {
            TrainOnTextIds = new() { "LEV" },
            PretranslateTextIds = new() { "MRK" }
        };

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
        Corpus corpus = env.DefaultParatextCorpus with
        {
            TrainOnTextIds = new() { "MAT" },
            PretranslateTextIds = new() { "MRK" }
        };
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
        Corpus corpus = env.DefaultParatextCorpus with
        {
            TrainOnTextIds = new() { "LEV" },
            PretranslateTextIds = new() { "MAT" }
        };
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
    public async Task ParallelCorpusLogic()
    {
        using TestEnvironment env = new();
        var corpora = new List<PreprocessBuildJob.ParallelCorpus>()
        {
            new PreprocessBuildJob.ParallelCorpus()
            {
                Id = "1",
                SourceCorpora = new List<PreprocessBuildJob.ParallelCorpusSubcorpus>()
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
                        PretranslateChapters = new()
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
                    },
                },
                TargetCorpora = new List<PreprocessBuildJob.ParallelCorpusSubcorpus>()
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
                                new() { }
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
                                new() { }
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
        await env.GetBuildJob(TranslationEngineType.SmtTransfer)
            .WriteDataFilesAsync("build1", corpora, "{\"use_key_terms\":false}", default);
        Assert.Multiple(async () =>
        {
            Assert.That(
                await env.GetSourceExtractAsync(),
                Is.EqualTo(
                    @"Source one, chapter fourteen, verse fifty-five. Segment b.
Source one, chapter fourteen, verse fifty-six.
Source one, chapter one, verse one.
Source two, chapter one, verse two.
Source two, chapter one, verse three.
Source two, chapter one, verse four.
Source two, chapter one, verse five. Source two, chapter one, verse six.
Source two, chapter one, verse seven. Source two, chapter one, verse eight.
Source two, chapter one, verse nine. Source two, chapter one, verse ten.
Source two, chapter one, verse one.
"
                )
            );
            Assert.That(
                await env.GetTargetExtractAsync(),
                Is.EqualTo(
                    @"Target two, chapter fourteen, verse fifty-five.
Target two, chapter fourteen, verse fifty-six.
Target one, chapter one, verse one.
Target one, chapter one, verse two.
Target one, chapter one, verse three.

Target one, chapter one, verse five and six.
Target one, chapter one, verse seven and eight.
Target one, chapter one, verse nine and ten.

"
                )
            );
        });
        JsonArray? pretranslations = await env.GetPretranslationsAsync();
        Assert.That(pretranslations, Is.Not.Null);
        Assert.That(pretranslations!.Count, Is.EqualTo(7), pretranslations.ToJsonString());
        Assert.That(
            pretranslations[2]!["translation"]!.ToString(),
            Is.EqualTo("Source one, chapter twelve, verse one.")
        );
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
        public IDistributedReaderWriterLockFactory LockFactory { get; }
        public IBuildJobService BuildJobService { get; }
        public IClearMLService ClearMLService { get; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }

        public Corpus DefaultTextFileCorpus { get; }
        public Corpus DefaultMixedSourceTextFileCorpus { get; }
        public Corpus DefaultParatextCorpus { get; }
        public Corpus DefaultMixedSourceParatextCorpus { get; }

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
                SourceLanguage = "es",
                TargetLanguage = "en",
                PretranslateTextIds = [],
                TrainOnTextIds = [],
                SourceFiles = [TextFile("source1")],
                TargetFiles = [TextFile("target1")]
            };

            DefaultMixedSourceTextFileCorpus = new()
            {
                Id = "corpusId1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                PretranslateTextIds = [],
                TrainOnTextIds = [],
                SourceFiles = [TextFile("source1"), TextFile("source2")],
                TargetFiles = [TextFile("target1")]
            };

            DefaultParatextCorpus = new()
            {
                Id = "corpusId1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                PretranslateTextIds = [],
                TrainOnTextIds = [],
                SourceFiles = [ParatextFile("pt-source1")],
                TargetFiles = [ParatextFile("pt-target1")]
            };

            DefaultMixedSourceParatextCorpus = new()
            {
                Id = "corpusId1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                PretranslateTextIds = [],
                TrainOnTextIds = [],
                SourceFiles = [ParatextFile("pt-source1"), ParatextFile("pt-source2")],
                TargetFiles = [ParatextFile("pt-target1")]
            };

            Engines = new MemoryRepository<TranslationEngine>();
            Engines.Add(
                new TranslationEngine
                {
                    Id = "engine1",
                    EngineId = "engine1",
                    Type = TranslationEngineType.Nmt,
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
                    Type = TranslationEngineType.Nmt,
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
                    Type = TranslationEngineType.Nmt,
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
            CorpusService = new CorpusService();
            PlatformService = Substitute.For<IPlatformService>();
            LockFactory = new DistributedReaderWriterLockFactory(
                new OptionsWrapper<ServiceOptions>(new ServiceOptions { ServiceId = "host" }),
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
                            TranslationEngineType = TranslationEngineType.Nmt,
                            ModelType = "huggingface",
                            DockerImage = "default",
                            Queue = "default"
                        },
                        new ClearMLBuildQueue()
                        {
                            TranslationEngineType = TranslationEngineType.SmtTransfer,
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
            BuildJobService = new BuildJobService(
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

        public PreprocessBuildJob GetBuildJob(TranslationEngineType engineType)
        {
            switch (engineType)
            {
                case TranslationEngineType.Nmt:
                {
                    return new NmtPreprocessBuildJob(
                        PlatformService,
                        Engines,
                        LockFactory,
                        new MemoryDataAccessContext(),
                        Substitute.For<ILogger<NmtPreprocessBuildJob>>(),
                        BuildJobService,
                        SharedFileService,
                        CorpusService,
                        new LanguageTagService()
                    )
                    {
                        Seed = 1234
                    };
                }
                case TranslationEngineType.SmtTransfer:
                {
                    return new PreprocessBuildJob(
                        PlatformService,
                        Engines,
                        LockFactory,
                        new MemoryDataAccessContext(),
                        Substitute.For<ILogger<PreprocessBuildJob>>(),
                        BuildJobService,
                        SharedFileService,
                        CorpusService
                    )
                    {
                        Seed = 1234
                    };
                }
                default:
                    throw new InvalidOperationException("Unknown engine type.");
            }
            ;
        }

        public Task RunBuildJobAsync(
            Corpus corpus,
            bool useKeyTerms = true,
            string engineId = "engine1",
            TranslationEngineType engineType = TranslationEngineType.Nmt
        )
        {
            return GetBuildJob(engineType)
                .RunAsync(engineId, "build1", [corpus], useKeyTerms ? null : "{\"use_key_terms\":false}", default);
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
            return (await GetPretranslationsAsync())?.Count ?? 0;
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
