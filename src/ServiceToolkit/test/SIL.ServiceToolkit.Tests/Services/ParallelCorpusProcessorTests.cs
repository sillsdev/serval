using SIL.ServiceToolkit.Services;

namespace SIL.ServiceToolkit.Utils;

[TestFixture]
public class ParallelCorpusPreprocessorTests
{
    private static readonly string TestDataPath = Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "Utils",
        "data"
    );

    [Test]
    public void TestParallelCorpusPreprocessor()
    {
        var processor = new ParallelCorpusPreprocessingService(new CorpusService());
        List<ParallelCorpus> corpora =
            new()
            {
                new()
                {
                    Id = "corpus1",
                    SourceCorpora = new List<MonolingualCorpus>
                    {
                        new MonolingualCorpus()
                        {
                            Id = "source-corpus1",
                            Language = "en",
                            Files = new List<CorpusFile>
                            {
                                new()
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "source1.txt")
                                }
                            }
                        },
                        new MonolingualCorpus()
                        {
                            Id = "source-corpus2",
                            Language = "en",
                            Files = new List<CorpusFile>
                            {
                                new()
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "source2.txt")
                                }
                            }
                        }
                    },
                    TargetCorpora = new List<MonolingualCorpus>
                    {
                        new MonolingualCorpus()
                        {
                            Id = "target-corpus1",
                            Language = "en",
                            Files = new List<CorpusFile>
                            {
                                new()
                                {
                                    TextId = "textId1",
                                    Format = FileFormat.Text,
                                    Location = Path.Combine(TestDataPath, "target1.txt")
                                }
                            }
                        }
                    }
                }
            };
        int trainCount = 0;
        int pretranslateCount = 0;
        processor.Preprocess(
            corpora,
            row =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    trainCount++;
            },
            (row, corpus) =>
            {
                pretranslateCount++;
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
