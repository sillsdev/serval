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
                    "0000",
                    "pt-source1",
                    "Original",
                    [
                        new()
                        {
                            NumAffectedVerses = 1,
                            Filename = "41MAT.SFM",
                            References = ["MAT 1:1"],
                            LineNumbers = [3],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Missing,
                        },
                        new()
                        {
                            NumAffectedVerses = 2,
                            Filename = "41MAT.SFM",
                            References = ["MAT 1:2", "MAT 1:3"],
                            LineNumbers = [4, 5],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Extra,
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

    [Test]
    public async Task ProcessTranslationRowsAsync()
    {
        PreprocessStats stats = new();

        var stream = Substitute.For<Stream>();
        stream.CanWrite.Returns(true);

        var sourceTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> sourceSegments = [];
        sourceTrainWriter.When(x => x.WriteAsync(Arg.Any<string>())).Do(c => sourceSegments.Add(c.ArgAt<string>(0)));

        var targetTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> targetSegments = [];
        targetTrainWriter.When(x => x.WriteAsync(Arg.Any<string>())).Do(c => targetSegments.Add(c.ArgAt<string>(0)));

        var sourceKeyTermsTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> sourceKeyTermSegments = [];
        sourceKeyTermsTrainWriter
            .When(x => x.WriteAsync(Arg.Any<string>()))
            .Do(c => sourceKeyTermSegments.Add(c.ArgAt<string>(0)));

        var targetKeyTermsTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> targetKeyTermSegments = [];
        targetKeyTermsTrainWriter
            .When(x => x.WriteAsync(Arg.Any<string>()))
            .Do(c => targetKeyTermSegments.Add(c.ArgAt<string>(0)));

        (ParallelRowContract Row, TrainingDataType Type)[] trainingRows =
        [
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1")],
                    [ScriptureRef.Parse("MAT 1:1")],
                    "Source Matthew 1:1",
                    "Target Matthew 1:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:2")],
                    [ScriptureRef.Parse("MAT 1:2")],
                    "Source Matthew 1:2",
                    "",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 2:1")],
                    [ScriptureRef.Parse("MAT 2:1")],
                    "Source Matthew 2:1",
                    "Target Matthew 2:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MRK",
                    [ScriptureRef.Parse("MRK 1:1")],
                    [ScriptureRef.Parse("MRK 1:1")],
                    "Source Mark 1:1",
                    "Target Mark 1:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MRK",
                    ["MajorBiblicalTerms:Isaac"],
                    ["MajorBiblicalTerms:Isaac"],
                    "Source Isaac",
                    "Target Isaac",
                    1
                ),
                TrainingDataType.KeyTerm
            ),
        ];
        foreach ((ParallelRowContract row, TrainingDataType type) in trainingRows)
        {
            await stats.ProcessTranslationTrainingRowAsync(
                row,
                type,
                sourceTrainWriter,
                targetTrainWriter,
                sourceKeyTermsTrainWriter,
                targetKeyTermsTrainWriter
            );
        }

        (ParallelRowContract Row, bool IsInTrainingData)[] pretranslateRows =
        [
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1/0:s")],
                    [ScriptureRef.Parse("MAT 1:1/0:s")],
                    "Source Matthew section header",
                    "",
                    1
                ),
                false
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1")],
                    [ScriptureRef.Parse("MAT 1:1")],
                    "Source Matthew 1:1",
                    "Target Matthew 1:1",
                    1
                ),
                true
            ),
            (
                new("JHN", [ScriptureRef.Parse("JHN 1:1")], [ScriptureRef.Parse("JHN 1:1")], "Source John 1:1", "", 1),
                false
            ),
        ];

        using MemoryStream memoryStream = new();
        using (Utf8JsonWriter pretranslateWriter = new(memoryStream))
        {
            pretranslateWriter.WriteStartArray();
            foreach ((ParallelRowContract row, bool isInTrainingData) in pretranslateRows)
            {
                await stats.ProcessPretranslateRowAsync(row, isInTrainingData, "corpus1", pretranslateWriter);
            }
            pretranslateWriter.WriteEndArray();
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        StreamReader pretranslationReader = new(memoryStream);
        string pretranslationContent = await pretranslationReader.ReadToEndAsync();

        Assert.That(
            sourceSegments.SequenceEqual([
                "Source Matthew 1:1\n",
                "Source Matthew 1:2\n",
                "Source Matthew 2:1\n",
                "Source Mark 1:1\n",
            ])
        );
        Assert.That(
            targetSegments.SequenceEqual(["Target Matthew 1:1\n", "\n", "Target Matthew 2:1\n", "Target Mark 1:1\n"])
        );
        Assert.That(sourceKeyTermSegments.SequenceEqual(["Source Isaac\n"]));
        Assert.That(targetKeyTermSegments.SequenceEqual(["Target Isaac\n"]));
        Assert.That(stats.TrainCount, Is.EqualTo(4));
        Assert.That(stats.TrainVerseCount, Has.Count.EqualTo(2));

        Assert.That(stats.TrainVerseCount, Does.ContainKey("MAT"));
        Assert.That(stats.TrainVerseCount["MAT"], Has.Count.EqualTo(2));
        Assert.That(stats.TrainVerseCount["MAT"], Does.ContainKey("1"));
        Assert.That(stats.TrainVerseCount["MAT"]["1"], Is.EqualTo(1));
        Assert.That(stats.TrainVerseCount["MAT"], Does.ContainKey("2"));
        Assert.That(stats.TrainVerseCount["MAT"]["2"], Is.EqualTo(1));

        Assert.That(stats.TrainVerseCount, Does.ContainKey("MRK"));
        Assert.That(stats.TrainVerseCount["MRK"], Has.Count.EqualTo(1));
        Assert.That(stats.TrainVerseCount["MRK"], Does.ContainKey("1"));
        Assert.That(stats.TrainVerseCount["MRK"]["1"], Is.EqualTo(1));

        Assert.That(
            pretranslationContent,
            Is.EqualTo(
                "["
                    + "{\"corpusId\":\"corpus1\",\"textId\":\"MAT\",\"sourceRefs\":[\"MAT 1:1/s\"],\"targetRefs\":[\"MAT 1:1/s\"],\"translation\":\"Source Matthew section header\"},"
                    + "{\"corpusId\":\"corpus1\",\"textId\":\"JHN\",\"sourceRefs\":[\"JHN 1:1\"],\"targetRefs\":[\"JHN 1:1\"],\"translation\":\"Source John 1:1\"}"
                    + "]"
            )
        );
        Assert.That(stats.InferenceCount, Is.EqualTo(2));
        Assert.That(stats.InferenceVerseCount, Has.Count.EqualTo(1));
        Assert.That(stats.InferenceVerseCount, Does.ContainKey("JHN"));
        Assert.That(stats.InferenceVerseCount["JHN"], Has.Count.EqualTo(1));
        Assert.That(stats.InferenceVerseCount["JHN"], Does.ContainKey("1"));
        Assert.That(stats.InferenceVerseCount["JHN"]["1"], Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessWordAlignmentRowsAsync()
    {
        PreprocessStats stats = new();

        var stream = Substitute.For<Stream>();
        stream.CanWrite.Returns(true);

        var sourceTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> sourceSegments = [];
        sourceTrainWriter.When(x => x.WriteAsync(Arg.Any<string>())).Do(c => sourceSegments.Add(c.ArgAt<string>(0)));

        var targetTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> targetSegments = [];
        targetTrainWriter.When(x => x.WriteAsync(Arg.Any<string>())).Do(c => targetSegments.Add(c.ArgAt<string>(0)));

        var sourceKeyTermsTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> sourceKeyTermSegments = [];
        sourceKeyTermsTrainWriter
            .When(x => x.WriteAsync(Arg.Any<string>()))
            .Do(c => sourceKeyTermSegments.Add(c.ArgAt<string>(0)));

        var targetKeyTermsTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> targetKeyTermSegments = [];
        targetKeyTermsTrainWriter
            .When(x => x.WriteAsync(Arg.Any<string>()))
            .Do(c => targetKeyTermSegments.Add(c.ArgAt<string>(0)));

        (ParallelRowContract Row, TrainingDataType Type)[] trainingRows =
        [
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1")],
                    [ScriptureRef.Parse("MAT 1:1")],
                    "Source Matthew 1:1",
                    "Target Matthew 1:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:2")],
                    [ScriptureRef.Parse("MAT 1:2")],
                    "Source Matthew 1:2",
                    "",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 2:1")],
                    [ScriptureRef.Parse("MAT 2:1")],
                    "Source Matthew 2:1",
                    "Target Matthew 2:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MRK",
                    [ScriptureRef.Parse("MRK 1:1")],
                    [ScriptureRef.Parse("MRK 1:1")],
                    "Source Mark 1:1",
                    "Target Mark 1:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MRK",
                    ["MajorBiblicalTerms:Isaac"],
                    ["MajorBiblicalTerms:Isaac"],
                    "Source Isaac",
                    "Target Isaac",
                    1
                ),
                TrainingDataType.KeyTerm
            ),
        ];
        foreach ((ParallelRowContract row, TrainingDataType type) in trainingRows)
        {
            await stats.ProcessWordAlignmentTrainingRowAsync(
                row,
                type,
                sourceTrainWriter,
                targetTrainWriter,
                sourceKeyTermsTrainWriter,
                targetKeyTermsTrainWriter
            );
        }

        (ParallelRowContract Row, bool IsInTrainingData)[] wordAlignRows =
        [
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1/0:s")],
                    [ScriptureRef.Parse("MAT 1:1/0:s")],
                    "Source Matthew section header",
                    "",
                    1
                ),
                false
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1")],
                    [ScriptureRef.Parse("MAT 1:1")],
                    "Source Matthew 1:1",
                    "Target Matthew 1:1",
                    1
                ),
                true
            ),
            (
                new("JHN", [ScriptureRef.Parse("JHN 1:1")], [ScriptureRef.Parse("JHN 1:1")], "Source John 1:1", "", 1),
                false
            ),
            (
                new(
                    "JHN",
                    [ScriptureRef.Parse("JHN 1:2")],
                    [ScriptureRef.Parse("JHN 1:2")],
                    "Source John 1:2",
                    "Target John 1:2",
                    1
                ),
                false
            ),
        ];

        using MemoryStream memoryStream = new();
        using (Utf8JsonWriter wordAlignmentWriter = new(memoryStream))
        {
            wordAlignmentWriter.WriteStartArray();
            foreach ((ParallelRowContract row, bool isInTrainingData) in wordAlignRows)
            {
                await stats.ProcessWordAlignRowAsync(row, isInTrainingData, "corpus1", wordAlignmentWriter);
            }
            wordAlignmentWriter.WriteEndArray();
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        StreamReader wordAlignmentReader = new(memoryStream);
        string wordAlignmentContent = await wordAlignmentReader.ReadToEndAsync();

        Assert.That(
            sourceSegments.SequenceEqual(["Source Matthew 1:1\n", "Source Matthew 2:1\n", "Source Mark 1:1\n"])
        );
        Assert.That(
            targetSegments.SequenceEqual(["Target Matthew 1:1\n", "Target Matthew 2:1\n", "Target Mark 1:1\n"])
        );
        Assert.That(sourceKeyTermSegments.SequenceEqual(["Source Isaac\n"]));
        Assert.That(targetKeyTermSegments.SequenceEqual(["Target Isaac\n"]));
        Assert.That(stats.TrainCount, Is.EqualTo(4));
        Assert.That(stats.TrainVerseCount, Has.Count.EqualTo(2));

        Assert.That(stats.TrainVerseCount, Does.ContainKey("MAT"));
        Assert.That(stats.TrainVerseCount["MAT"], Has.Count.EqualTo(2));
        Assert.That(stats.TrainVerseCount["MAT"], Does.ContainKey("1"));
        Assert.That(stats.TrainVerseCount["MAT"]["1"], Is.EqualTo(1));
        Assert.That(stats.TrainVerseCount["MAT"], Does.ContainKey("2"));
        Assert.That(stats.TrainVerseCount["MAT"]["2"], Is.EqualTo(1));

        Assert.That(stats.TrainVerseCount, Does.ContainKey("MRK"));
        Assert.That(stats.TrainVerseCount["MRK"], Has.Count.EqualTo(1));
        Assert.That(stats.TrainVerseCount["MRK"], Does.ContainKey("1"));
        Assert.That(stats.TrainVerseCount["MRK"]["1"], Is.EqualTo(1));

        Assert.That(
            wordAlignmentContent,
            Is.EqualTo(
                "["
                    + "{\"corpusId\":\"corpus1\",\"textId\":\"JHN\",\"sourceRefs\":[\"JHN 1:2\"],\"targetRefs\":[\"JHN 1:2\"],\"source\":\"Source John 1:2\",\"target\":\"Target John 1:2\"}"
                    + "]"
            )
        );
        Assert.That(stats.InferenceCount, Is.EqualTo(1));
        Assert.That(stats.InferenceVerseCount, Has.Count.EqualTo(1));
        Assert.That(stats.InferenceVerseCount, Does.ContainKey("JHN"));
        Assert.That(stats.InferenceVerseCount["JHN"], Has.Count.EqualTo(1));
        Assert.That(stats.InferenceVerseCount["JHN"], Does.ContainKey("1"));
        Assert.That(stats.InferenceVerseCount["JHN"]["1"], Is.EqualTo(1));
    }

    private class TestEnvironment
    {
        public ISharedFileService SharedFileService { get; }
        public IPlatformService PlatformService { get; }
        public MemoryRepository<TranslationEngine> Engines { get; }
        public MemoryRepository<TrainSegmentPair> TrainSegmentPairs { get; }
        public IBuildJobService<TranslationEngine> BuildJobService { get; }
        public IClearMLService ClearMLService { get; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }
        public IParallelCorpusService ParallelCorpusService { get; }
        public SmtTransferEngineStateService StateService { get; private set; }

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
                        BuildJobRunner = BuildJobRunnerType.Local,
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
                        BuildJobRunner = BuildJobRunnerType.Local,
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
                        BuildJobRunner = BuildJobRunnerType.Local,
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
                        BuildJobRunner = BuildJobRunnerType.Local,
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
                    new LocalBuildJobRunner(
                        [new NmtLocalBuildJobFactory(), new SmtTransferLocalBuildJobFactory()],
                        Substitute.For<IServiceScopeFactory>(),
                        Substitute.For<ILogger<LocalBuildJobRunner>>()
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
            StateService = CreateStateService();
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
                        StateService,
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

        private static SmtTransferEngineStateService CreateStateService()
        {
            var options = Substitute.For<IOptionsMonitor<SmtTransferEngineOptions>>();
            options.CurrentValue.Returns(new SmtTransferEngineOptions());
            return new SmtTransferEngineStateService(
                Substitute.For<ISmtModelFactory>(),
                Substitute.For<ITransferEngineFactory>(),
                Substitute.For<ITruecaserFactory>(),
                options,
                Substitute.For<ILogger<SmtTransferEngineStateService>>()
            );
        }
    }
}
