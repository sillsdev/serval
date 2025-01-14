namespace SIL.ServiceToolkit.Services;

[TestFixture]
public class ParallelCorpusPreprocessingServiceTests
{
    private static readonly string TestDataPath = Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "Services",
        "data"
    );

    [Test]
    public async Task TestParallelCorpusPreprocessor()
    {
        ParallelCorpusPreprocessingService processor = new(new CorpusService());
        List<ParallelCorpus> corpora =
        [
            new()
            {
                Id = "corpus1",
                SourceCorpora =
                [
                    new()
                    {
                        Id = "source-corpus1",
                        Language = "en",
                        Files =
                        [
                            new()
                            {
                                TextId = "textId1",
                                Format = FileFormat.Text,
                                Location = Path.Combine(TestDataPath, "source1.txt")
                            }
                        ]
                    },
                    new()
                    {
                        Id = "source-corpus2",
                        Language = "en",
                        Files =
                        [
                            new()
                            {
                                TextId = "textId1",
                                Format = FileFormat.Text,
                                Location = Path.Combine(TestDataPath, "source2.txt")
                            }
                        ]
                    }
                ],
                TargetCorpora =
                [
                    new()
                    {
                        Id = "target-corpus1",
                        Language = "en",
                        Files =
                        [
                            new()
                            {
                                TextId = "textId1",
                                Format = FileFormat.Text,
                                Location = Path.Combine(TestDataPath, "target1.txt")
                            }
                        ]
                    }
                ]
            }
        ];
        int trainCount = 0;
        int pretranslateCount = 0;
        await processor.PreprocessAsync(
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
                    pretranslateCount++;
                }

                return Task.CompletedTask;
            },
            false
        );

        Assert.Multiple(() =>
        {
            Assert.That(trainCount, Is.EqualTo(2));
            Assert.That(pretranslateCount, Is.EqualTo(3));
        });
    }
}
