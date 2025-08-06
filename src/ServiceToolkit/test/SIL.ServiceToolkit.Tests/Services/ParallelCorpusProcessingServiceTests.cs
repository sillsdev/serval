namespace SIL.ServiceToolkit.Services;

[TestFixture]
public class ParallelCorpusPreprocessingServiceTests
{
    [Test]
    public void TestParallelCorpusAnalysis_FileFormatParatext()
    {
        using var env = new TestEnvironment();
        ParallelCorpus parallelCorpus = env.GetCorpus(paratextProject: true);
        const string ExpectedSourceName = "standard_english";
        const string ExpectedTargetName = "typewriter_english";

        (QuoteConventionAnalysis? sourceQuotationConvention, QuoteConventionAnalysis? targetQuotationConvention) =
            env.Processor.AnalyzeParallelCorpus(parallelCorpus);

        Assert.Multiple(() =>
        {
            Assert.That(sourceQuotationConvention, Is.Not.Null);
            Assert.That(sourceQuotationConvention!.BestQuoteConvention.Name, Is.EqualTo(ExpectedSourceName));
            Assert.That(targetQuotationConvention, Is.Not.Null);
            Assert.That(targetQuotationConvention!.BestQuoteConvention.Name, Is.EqualTo(ExpectedTargetName));
        });
    }

    [Test]
    public void TestParallelCorpusAnalysis_FileFormatText()
    {
        using var env = new TestEnvironment();
        ParallelCorpus parallelCorpus = env.GetCorpus(paratextProject: false);

        (QuoteConventionAnalysis? sourceQuotationConvention, QuoteConventionAnalysis? targetQuotationConvention) =
            env.Processor.AnalyzeParallelCorpus(parallelCorpus);

        Assert.Multiple(() =>
        {
            Assert.That(sourceQuotationConvention, Is.Null);
            Assert.That(targetQuotationConvention, Is.Null);
        });
    }

    [Test]
    public async Task TestParallelCorpusPreprocessor()
    {
        using var env = new TestEnvironment();
        IReadOnlyList<ParallelCorpus> corpora = [env.GetCorpus(paratextProject: false)];
        int trainCount = 0;
        int inferenceCount = 0;
        await env.Processor.PreprocessAsync(
            corpora,
            row =>
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
        private readonly TempDirectory _tempDir = new TempDirectory(name: "ParallelCorpusProcessingServiceTests");

        public IParallelCorpusPreprocessingService Processor { get; } =
            new ParallelCorpusPreprocessingService(new CorpusService());

        public ParallelCorpus GetCorpus(bool paratextProject)
        {
            if (paratextProject)
            {
                return new ParallelCorpus
                {
                    Id = "corpus1",
                    SourceCorpora =
                    [
                        new MonolingualCorpus
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
                        },
                    ],
                    TargetCorpora =
                    [
                        new MonolingualCorpus
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
                };
            }

            return new ParallelCorpus
            {
                Id = "corpus1",
                SourceCorpora =
                [
                    new MonolingualCorpus
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
                    new MonolingualCorpus
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
                    new MonolingualCorpus
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
            };
        }

        protected override void DisposeManagedResources()
        {
            _tempDir.Dispose();
        }

        private string ZipParatextProject(string name)
        {
            string fileName = Path.Combine(_tempDir.Path, $"{name}.zip");
            ZipFile.CreateFromDirectory(Path.Combine(TestDataPath, name), fileName);
            return fileName;
        }
    }
}
