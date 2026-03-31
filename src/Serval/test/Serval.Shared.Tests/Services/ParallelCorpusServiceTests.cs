namespace Serval.Shared.Services;

[TestFixture]
public class ParallelCorpusServiceTests
{
    [Test]
    public void TestParallelCorpusAnalysis_FileFormatParatext()
    {
        using var env = new TestEnvironment();
        FilteredParallelCorpus parallelCorpus = env.GetCorpora(paratextProject: true).First();
        const string ExpectedTargetName = "typewriter_english";

        QuoteConventionAnalysis? targetQuotationConvention = env.Processor.AnalyzeTargetQuoteConvention([
            parallelCorpus,
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(targetQuotationConvention, Is.Not.Null);
            Assert.That(targetQuotationConvention!.BestQuoteConvention.Name, Is.EqualTo(ExpectedTargetName));
        });
    }

    [Test]
    public void TestParallelCorpusAnalysis_FileFormatText()
    {
        using var env = new TestEnvironment();
        FilteredParallelCorpus parallelCorpus = env.GetCorpora(paratextProject: false).First();

        QuoteConventionAnalysis? targetQuotationConvention = env.Processor.AnalyzeTargetQuoteConvention([
            parallelCorpus,
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(targetQuotationConvention, Is.Not.Null);
            Assert.That(targetQuotationConvention!.BestQuoteConvention, Is.Null);
        });
    }

    [Test]
    public async Task TestPreprocess_FileFormatText()
    {
        using var env = new TestEnvironment();
        IReadOnlyList<FilteredParallelCorpus> corpora = env.GetCorpora(paratextProject: false);
        int trainCount = 0;
        int inferenceCount = 0;
        await env.Processor.PreprocessAsync(
            corpora,
            (row, _) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    trainCount++;
                return Task.CompletedTask;
            },
            (row, isInTrainingData, _) =>
            {
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                {
                    inferenceCount++;
                }

                return Task.CompletedTask;
            },
            false
        );

        Assert.Multiple(() =>
        {
            Assert.That(trainCount, Is.EqualTo(2));
            Assert.That(inferenceCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task TestPreprocess_FileFormatParatext()
    {
        using var env = new TestEnvironment();
        IReadOnlyList<FilteredParallelCorpus> corpora = env.GetCorpora(paratextProject: true);
        int trainCount = 0;
        int inferenceCount = 0;
        var trainRefs = new List<string>();
        var inferenceRefs = new List<string>();
        await env.Processor.PreprocessAsync(
            corpora,
            (row, _) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    trainCount++;
                    trainRefs.Add(row.TargetRefs[0].ToString() ?? "");
                }
                return Task.CompletedTask;
            },
            (row, isInTrainingData, _) =>
            {
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                {
                    inferenceCount++;
                    inferenceRefs.Add(row.TargetRefs[0].ToString() ?? "");
                }

                return Task.CompletedTask;
            },
            false,
            ["mt"]
        );

        Assert.Multiple(() =>
        {
            Assert.That(trainCount, Is.EqualTo(5));
            Assert.That(inferenceCount, Is.EqualTo(16));
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
        private readonly TempDirectory _tempDir = new TempDirectory(name: "ParallelCorpusServiceTests");

        public IParallelCorpusService Processor { get; } = new ParallelCorpusService();

        public FilteredParallelCorpus[] GetCorpora(bool paratextProject)
        {
            if (paratextProject)
            {
                return
                [
                    new FilteredParallelCorpus
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new FilteredMonolingualCorpus
                            {
                                Id = "pt-source1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-source1"),
                                    },
                                ],
                                InferenceTextIds = [],
                            },
                        ],
                        TargetCorpora =
                        [
                            new FilteredMonolingualCorpus
                            {
                                Id = "pt-target1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-target1"),
                                    },
                                ],
                            },
                        ],
                    },
                    new FilteredParallelCorpus
                    {
                        Id = "corpus2",
                        SourceCorpora =
                        [
                            new FilteredMonolingualCorpus
                            {
                                Id = "pt-source1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-source1"),
                                    },
                                ],
                                TrainOnTextIds = [],
                            },
                        ],
                        TargetCorpora =
                        [
                            new FilteredMonolingualCorpus
                            {
                                Id = "pt-target1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-target1"),
                                    },
                                ],
                                TrainOnTextIds = [],
                            },
                        ],
                    },
                ];
            }

            return
            [
                new FilteredParallelCorpus
                {
                    Id = "corpus1",
                    SourceCorpora =
                    [
                        new FilteredMonolingualCorpus
                        {
                            Id = "source-corpus1",
                            Language = "en",
                            Files =
                            [
                                new CorpusFile
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "source1.txt"),
                                },
                            ],
                        },
                        new FilteredMonolingualCorpus
                        {
                            Id = "source-corpus2",
                            Language = "en",
                            Files =
                            [
                                new CorpusFile
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "source2.txt"),
                                },
                            ],
                        },
                    ],
                    TargetCorpora =
                    [
                        new FilteredMonolingualCorpus
                        {
                            Id = "target-corpus1",
                            Language = "en",
                            Files =
                            [
                                new CorpusFile
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "target1.txt"),
                                },
                            ],
                        },
                    ],
                },
            ];
        }

        protected override void DisposeManagedResources()
        {
            _tempDir.Dispose();
        }

        private string ZipParatextProject(string name)
        {
            string fileName = Path.Combine(_tempDir.Path, $"{name}.zip");
            if (!File.Exists(fileName))
                ZipFile.CreateFromDirectory(Path.Combine(TestDataPath, name), fileName);
            return fileName;
        }
    }
}
