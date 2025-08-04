namespace SIL.ServiceToolkit.Services;

[TestFixture]
public class ParallelCorpusPreprocessingServiceTests
{
    [Test]
    public async Task TestParallelCorpusAnalysis()
    {
        using var env = new TestEnvironment();
        IReadOnlyList<ParallelCorpus> corpora = env.GetCorpora(includeParatextProjects: true);
        List<(string corpusRef, string sourceQuoteConvention, string targetQuoteConvention)> expected =
        [
            // Parallel corpus with corpora containing text files
            (corpusRef: "corpus1", sourceQuoteConvention: string.Empty, targetQuoteConvention: string.Empty),
            // Parallel corpus with corpora containing Paratext zip files
            (
                corpusRef: "corpus2",
                sourceQuoteConvention: "standard_english",
                targetQuoteConvention: "typewriter_english"
            ),
        ];
        List<(string corpusRef, string sourceQuoteConvention, string targetQuoteConvention)> corpusAnalysis = [];
        await env.Processor.AnalyseCorporaAsync(
            corpora,
            async (sourceQuotationConvention, targetQuotationConvention, corpus) =>
            {
                corpusAnalysis.Add(
                    (
                        corpusRef: corpus.Id,
                        sourceQuoteConvention: sourceQuotationConvention?.BestQuoteConvention.Name ?? string.Empty,
                        targetQuoteConvention: targetQuotationConvention?.BestQuoteConvention.Name ?? string.Empty
                    )
                );
                await Task.CompletedTask;
            }
        );

        Assert.That(corpusAnalysis, Is.EqualTo(expected));
    }

    [Test]
    public async Task TestParallelCorpusPreprocessor()
    {
        using var env = new TestEnvironment();
        IReadOnlyList<ParallelCorpus> corpora = env.GetCorpora(includeParatextProjects: false);
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

        public IReadOnlyList<ParallelCorpus> GetCorpora(bool includeParatextProjects)
        {
            List<ParallelCorpus> corpora =
            [
                new ParallelCorpus
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
                                    Location = Path.Combine(TestDataPath, "source1.txt")
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
                                    Location = Path.Combine(TestDataPath, "source2.txt")
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
                                    Location = Path.Combine(TestDataPath, "target1.txt")
                                },
                            ],
                        },
                    ],
                },
            ];

            if (includeParatextProjects)
            {
                corpora.Add(
                    new ParallelCorpus
                    {
                        Id = "corpus2",
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
                    }
                );
            }

            return corpora;
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
