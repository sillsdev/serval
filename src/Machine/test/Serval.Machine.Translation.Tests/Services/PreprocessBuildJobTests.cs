namespace Serval.Machine.Translation.Services;

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

        env.ParallelCorpusService.PreprocessAsync(
                Arg.Any<IEnumerable<ParallelCorpusContract>>(),
                Arg.Any<Func<ParallelRowContract, TrainingDataType, Task>>(),
                Arg.Any<Func<ParallelRowContract, bool, string, Task>>(),
                Arg.Any<bool>(),
                Arg.Any<HashSet<string>>()
            )
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
                ci.ArgAt<Func<ParallelRowContract, TrainingDataType, Task>>(1)
                    .Invoke(
                        new ParallelRowContract(
                            "MAT",
                            [ScriptureRef.Parse("MAT 1:1")],
                            [ScriptureRef.Parse("MAT 1:1")],
                            "Source Matthew 1:1",
                            "Target Matthew 1:1",
                            1
                        ),
                        TrainingDataType.Text
                    )
            );

        env.ParallelCorpusService.AnalyzeUsfmVersification(Arg.Any<IEnumerable<ParallelCorpusContract>>())
            .Returns([
                (
                    "corpusId1",
                    "src_1",
                    "pt-source1",
                    "0000",
                    "Original",
                    [
                        new()
                        {
                            NumAffectedVerses = 2,
                            Filename = "41MAT.SFM",
                            References = ["MAT 1:2", "MAT 1:3"],
                            LineNumbers = [4, 5],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Extra,
                        },
                        new()
                        {
                            NumAffectedVerses = 1,
                            Filename = "41MAT.SFM",
                            References = ["MAT 1:1"],
                            LineNumbers = [3],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Missing,
                        },
                    ]
                ),
                (
                    "corpusId1",
                    "trg_1",
                    "pt-target1",
                    "1111",
                    "English",
                    [
                        new()
                        {
                            NumAffectedVerses = 1,
                            Filename = "41MAT.SFM",
                            References = ["MAT 1:4a"],
                            LineNumbers = [6],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.IncorrectVerseSegment,
                        },
                        new()
                        {
                            NumAffectedVerses = 1,
                            Filename = "41MAT.SFM",
                            References = ["MAT :1"],
                            LineNumbers = [12],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.InvalidChapter,
                        },
                        new()
                        {
                            NumAffectedVerses = 1,
                            Filename = "41MAT.SFM",
                            References = ["MAT 2:1$"],
                            LineNumbers = [13],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.InvalidVerse,
                        },
                        new()
                        {
                            NumAffectedVerses = 15,
                            Filename = "41MAT.SFM",
                            References = ["MAT 2:2-16"],
                            LineNumbers = [20],
                            Type = Serval.Shared.Contracts.UsfmVersificationDiagnosticType.UnsupportedVerseRange,
                        },
                    ]
                ),
            ]);

        env.ParallelCorpusService.FindMissingParentProjects(Arg.Any<IEnumerable<ParallelCorpusContract>>())
            .Returns([
                (
                    "corpusId1",
                    "src_1",
                    new MissingParentProjectErrorContract
                    {
                        ProjectName = "pt-source1",
                        ProjectGuid = "0000",
                        ParentProjectGuid = "1111",
                        ParentProjectName = "pt-source1-parent",
                    }
                ),
            ]);

        env.LanguageTagService.ConvertToFlores200Code(Arg.Any<string>(), out Arg.Any<string>())
            .Returns(Flores200Support.None);

        await env.RunBuildJobAsync(corpus1, engineId: "engine4");
        Assert.That(env.ExecutionData.Warnings, Has.Count.EqualTo(11));
        Assert.That(env.ExecutionData.Diagnostics, Has.Count.EqualTo(11));
        Assert.That(env.ExecutionData.DiagnosticsTruncated, Is.False);

        Assert.That(env.ExecutionData.Diagnostics[0].Code, Is.EqualTo("USFM-0003"));
        Dictionary<string, object> data = env.ExecutionData.Diagnostics[0].Data;
        Assert.That(data, Has.Count.EqualTo(8));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["numberOfVerses"] is int numberOfVerses && numberOfVerses == 2);
            Assert.That(data["projectName"] is string projectName && projectName == "pt-source1");
            Assert.That(data["projectGuid"] is string projectGuid && projectGuid == "0000");
            Assert.That(data["usfmFilename"] is string usfmFilename && usfmFilename == "41MAT.SFM");
            Assert.That(data["lineNumbers"] is List<int> lineNumbers && lineNumbers.SequenceEqual([4, 5]));
            Assert.That(
                data["verseReferences"] is List<string> verseReferences
                    && verseReferences.SequenceEqual(["MAT 1:2", "MAT 1:3"])
            );
            Assert.That(data["parallelCorpusId"] is string parallelCorpusId && parallelCorpusId == "corpusId1");
            Assert.That(data["monolingualCorpusId"] is string monolingualCorpusId && monolingualCorpusId == "src_1");
        }

        Assert.That(env.ExecutionData.Diagnostics[1].Code, Is.EqualTo("USFM-0004"));
        data = env.ExecutionData.Diagnostics[1].Data;
        Assert.That(data, Has.Count.EqualTo(8));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["numberOfVerses"] is int numberOfVerses && numberOfVerses == 1);
            Assert.That(data["projectName"] is string projectName && projectName == "pt-source1");
            Assert.That(data["projectGuid"] is string projectGuid && projectGuid == "0000");
            Assert.That(data["usfmFilename"] is string usfmFilename && usfmFilename == "41MAT.SFM");
            Assert.That(data["lineNumbers"] is List<int> lineNumbers && lineNumbers.SequenceEqual([3]));
            Assert.That(
                data["verseReferences"] is List<string> verseReferences && verseReferences.SequenceEqual(["MAT 1:1"])
            );
            Assert.That(data["parallelCorpusId"] is string parallelCorpusId && parallelCorpusId == "corpusId1");
            Assert.That(data["monolingualCorpusId"] is string monolingualCorpusId && monolingualCorpusId == "src_1");
        }

        Assert.That(env.ExecutionData.Diagnostics[2].Code, Is.EqualTo("USFM-0005"));
        data = env.ExecutionData.Diagnostics[2].Data;
        Assert.That(data, Has.Count.EqualTo(7));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["projectName"] is string projectName && projectName == "pt-target1");
            Assert.That(data["projectGuid"] is string projectGuid && projectGuid == "1111");
            Assert.That(data["usfmFilename"] is string usfmFilename && usfmFilename == "41MAT.SFM");
            Assert.That(data["lineNumber"] is int lineNumber && lineNumber == 6);
            Assert.That(data["verseReference"] is string verseReference && verseReference == "MAT 1:4a");
            Assert.That(data["parallelCorpusId"] is string parallelCorpusId && parallelCorpusId == "corpusId1");
            Assert.That(data["monolingualCorpusId"] is string monolingualCorpusId && monolingualCorpusId == "trg_1");
        }

        Assert.That(env.ExecutionData.Diagnostics[3].Code, Is.EqualTo("USFM-0001"));
        data = env.ExecutionData.Diagnostics[3].Data;
        Assert.That(data, Has.Count.EqualTo(7));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["projectName"] is string projectName && projectName == "pt-target1");
            Assert.That(data["projectGuid"] is string projectGuid && projectGuid == "1111");
            Assert.That(data["usfmFilename"] is string usfmFilename && usfmFilename == "41MAT.SFM");
            Assert.That(data["lineNumber"] is int lineNumber && lineNumber == 12);
            Assert.That(data["verseReference"] is string verseReference && verseReference == "MAT :1");
            Assert.That(data["parallelCorpusId"] is string parallelCorpusId && parallelCorpusId == "corpusId1");
            Assert.That(data["monolingualCorpusId"] is string monolingualCorpusId && monolingualCorpusId == "trg_1");
        }

        Assert.That(env.ExecutionData.Diagnostics[4].Code, Is.EqualTo("USFM-0002"));
        data = env.ExecutionData.Diagnostics[4].Data;
        Assert.That(data, Has.Count.EqualTo(7));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["projectName"] is string projectName && projectName == "pt-target1");
            Assert.That(data["projectGuid"] is string projectGuid && projectGuid == "1111");
            Assert.That(data["usfmFilename"] is string usfmFilename && usfmFilename == "41MAT.SFM");
            Assert.That(data["lineNumber"] is int lineNumber && lineNumber == 13);
            Assert.That(data["verseReference"] is string verseReference && verseReference == "MAT 2:1$");
            Assert.That(data["parallelCorpusId"] is string parallelCorpusId && parallelCorpusId == "corpusId1");
            Assert.That(data["monolingualCorpusId"] is string monolingualCorpusId && monolingualCorpusId == "trg_1");
        }

        Assert.That(env.ExecutionData.Diagnostics[5].Code, Is.EqualTo("USFM-0006"));
        data = env.ExecutionData.Diagnostics[5].Data;
        Assert.That(data, Has.Count.EqualTo(7));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["projectName"] is string projectName && projectName == "pt-target1");
            Assert.That(data["projectGuid"] is string projectGuid && projectGuid == "1111");
            Assert.That(data["usfmFilename"] is string usfmFilename && usfmFilename == "41MAT.SFM");
            Assert.That(data["lineNumber"] is int lineNumber && lineNumber == 20);
            Assert.That(data["verseReference"] is string verseReference && verseReference == "MAT 2:2-16");
            Assert.That(data["parallelCorpusId"] is string parallelCorpusId && parallelCorpusId == "corpusId1");
            Assert.That(data["monolingualCorpusId"] is string monolingualCorpusId && monolingualCorpusId == "trg_1");
        }

        Assert.That(env.ExecutionData.Diagnostics[6].Code, Is.EqualTo("CONFIG-0001"));
        data = env.ExecutionData.Diagnostics[6].Data;
        Assert.That(data, Has.Count.EqualTo(6));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                data["parentProjectName"] is string parentProjectName && parentProjectName == "pt-source1-parent"
            );
            Assert.That(data["parentProjectGuid"] is string parentProjectGuid && parentProjectGuid == "1111");
            Assert.That(
                data["daughterProjectName"] is string daughterProjectName && daughterProjectName == "pt-source1"
            );
            Assert.That(data["daughterProjectGuid"] is string daughterProjectGuid && daughterProjectGuid == "0000");
            Assert.That(data["parallelCorpusId"] is string parallelCorpusId && parallelCorpusId == "corpusId1");
            Assert.That(data["monolingualCorpusId"] is string monolingualCorpusId && monolingualCorpusId == "src_1");
        }

        Assert.That(env.ExecutionData.Diagnostics[7].Code, Is.EqualTo("CONFIG-0002"));
        data = env.ExecutionData.Diagnostics[7].Data;
        Assert.That(data, Has.Count.EqualTo(1));
        Assert.That(
            data["projectVersifications"] is Dictionary<string, string> projectVersifications
                && projectVersifications.Count == 2
                && projectVersifications["0000"] == "Original"
                && projectVersifications["1111"] == "English"
        );

        Assert.That(env.ExecutionData.Diagnostics[8].Code, Is.EqualTo("CONFIG-0003"));
        data = env.ExecutionData.Diagnostics[8].Data;
        Assert.That(data, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["trainCount"] is int trainCount && trainCount == 1);
            Assert.That(data["minimumTrainCount"] is int minimumTrainCount && minimumTrainCount == 600);
        }

        Assert.That(env.ExecutionData.Diagnostics[9].Code, Is.EqualTo("MODEL-0001"));
        data = env.ExecutionData.Diagnostics[9].Data;
        Assert.That(data, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["resolvedCode"], Is.Null);
            Assert.That(data["modelName"] is string modelName && modelName == "NLLB");
        }

        Assert.That(env.ExecutionData.Diagnostics[10].Code, Is.EqualTo("MODEL-0002"));
        data = env.ExecutionData.Diagnostics[10].Data;
        Assert.That(data, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["resolvedCode"], Is.Null);
            Assert.That(data["modelName"] is string modelName && modelName == "NLLB");
        }

        env.BuildJobOptions.CurrentValue.Returns(new BuildJobOptions() { MaxWarnings = 1, MaxDiagnostics = 1 });
        await env.RunBuildJobAsync(corpus1, engineId: "engine4");
        // Two warnings after truncation + one warning mentioning that warnings were truncated
        Assert.That(env.ExecutionData.Warnings, Has.Count.EqualTo(2));
        Assert.That(env.ExecutionData.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(env.ExecutionData.DiagnosticsTruncated, Is.True);

        Assert.That(env.ExecutionData.Diagnostics[0].Code, Is.EqualTo("USFM-0004"));

        env.ParallelCorpusService.ClearSubstitute();
        env.ParallelCorpusService.AnalyzeUsfmVersification(Arg.Any<IEnumerable<ParallelCorpusContract>>()).Returns([]);
        env.ParallelCorpusService.FindMissingParentProjects(Arg.Any<IEnumerable<ParallelCorpusContract>>()).Returns([]);

        env.ParallelCorpusService.PreprocessAsync(
                Arg.Any<IEnumerable<ParallelCorpusContract>>(),
                Arg.Any<Func<ParallelRowContract, TrainingDataType, Task>>(),
                Arg.Any<Func<ParallelRowContract, bool, string, Task>>(),
                Arg.Any<bool>(),
                Arg.Any<HashSet<string>>()
            )
            .Returns(Task.CompletedTask);

        env.BuildJobOptions.ClearSubstitute();
        env.BuildJobOptions.CurrentValue.Returns(new BuildJobOptions() { MaxWarnings = 1_000, MaxDiagnostics = 1_000 });

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await env.RunBuildJobAsync(corpus1, engineId: "engine1");
        });

        Assert.That(env.ExecutionData.DiagnosticsTruncated, Is.False);
        Assert.That(env.ExecutionData.Diagnostics, Has.Count.EqualTo(4));

        Assert.That(env.ExecutionData.Diagnostics[0].Code, Is.EqualTo("CONFIG-0004"));
        data = env.ExecutionData.Diagnostics[0].Data;
        Assert.That(data, Is.Empty);

        Assert.That(env.ExecutionData.Diagnostics[1].Code, Is.EqualTo("MODEL-0001"));
        data = env.ExecutionData.Diagnostics[1].Data;
        Assert.That(data, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["resolvedCode"], Is.Null);
            Assert.That(data["modelName"] is string modelName && modelName == "NLLB");
        }

        Assert.That(env.ExecutionData.Diagnostics[2].Code, Is.EqualTo("MODEL-0002"));
        data = env.ExecutionData.Diagnostics[2].Data;
        Assert.That(data, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["resolvedCode"], Is.Null);
            Assert.That(data["modelName"] is string modelName && modelName == "NLLB");
        }

        Assert.That(env.ExecutionData.Diagnostics[3].Code, Is.EqualTo("MODEL-0004"));
        data = env.ExecutionData.Diagnostics[3].Data;
        Assert.That(data, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["modelName"] is string modelName && modelName == "NLLB");
            Assert.That(
                data["unknownLanguageCodes"] is List<string> unknownLanguageCodes
                    && unknownLanguageCodes.SequenceEqual(["es", "en"])
            );
        }
    }

    [Test]
    public void RunAsync_UnknownLanguageTagsNoData()
    {
        TestEnvironment env = new();
        env.LanguageTagService.ConvertToFlores200Code("xxx", out Arg.Any<string>())
            .Returns(Flores200Support.OnlyScript);
        env.LanguageTagService.ConvertToFlores200Code("zzz", out Arg.Any<string>()).Returns(Flores200Support.None);
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
        env.LanguageTagService.ConvertToFlores200Code("xxx", out Arg.Any<string>())
            .Returns(Flores200Support.OnlyScript);
        env.LanguageTagService.ConvertToFlores200Code("zzz", out Arg.Any<string>()).Returns(Flores200Support.None);
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

    private class TestEnvironment
    {
        public ISharedFileService SharedFileService { get; }
        public IPlatformService PlatformService { get; }
        public MemoryRepository<TranslationEngine> Engines { get; }
        public MemoryRepository<TrainSegmentPair> TrainSegmentPairs { get; }
        public IBuildJobService<TranslationEngine> BuildJobService { get; }
        public IClearMLService ClearMLService { get; }
        public IOptionsMonitor<BuildJobOptions> BuildJobOptions { get; }
        public ILanguageTagService LanguageTagService { get; }
        public IParallelCorpusService ParallelCorpusService { get; }
        public IBuildDiagnosticService BuildDiagnosticService { get; }
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
                        Model = Models.Models.Nllb,
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
                        Model = Models.Models.Nllb,
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
            LanguageTagService = Substitute.For<ILanguageTagService>();
            BuildJobService = new BuildJobService<TranslationEngine>(
                [
                    new TranslationEngineLocalBuildJobRunner(
                        [new NmtLocalBuildJobFactory(), new SmtTransferLocalBuildJobFactory()],
                        Substitute.For<IServiceScopeFactory>(),
                        Substitute.For<ILogger<TranslationEngineLocalBuildJobRunner>>()
                    ),
                    new ClearMLBuildJobRunner<TranslationEngine>(
                        ClearMLService,
                        [
                            new NmtClearMLBuildJobFactory(SharedFileService, LanguageTagService, Engines),
                            new SmtTransferClearMLBuildJobFactory(SharedFileService, Engines),
                        ],
                        BuildJobOptions
                    ),
                ],
                Engines
            );
            ParallelCorpusService = Substitute.For<IParallelCorpusService>();
            BuildDiagnosticService = Substitute.For<IBuildDiagnosticService>();
            BuildDiagnosticService
                .CreateDiagnostic(Arg.Any<string>(), Arg.Any<Dictionary<string, object>>())
                .Returns(ci =>
                {
                    string code = ci.ArgAt<string>(0);
                    return new DiagnosticContract
                    {
                        Code = code,
                        Message = "",
                        Category = "",
                        Data = ci.ArgAt<Dictionary<string, object>>(1),
                        //So that we can confirm that higher severity diagnostics are preserved when truncating
                        Severity = code == "USFM-0003" ? DiagnosticSeverity.Info : DiagnosticSeverity.Warn,
                    };
                });
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
                        LanguageTagService,
                        ParallelCorpusService,
                        BuildDiagnosticService,
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
                        BuildDiagnosticService,
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
