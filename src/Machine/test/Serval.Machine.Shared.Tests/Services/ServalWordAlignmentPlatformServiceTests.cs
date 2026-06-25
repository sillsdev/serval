namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ServalWordAlignmentPlatformServiceTests
{
    [Test]
    public async Task InsertWordAlignmentsAsync_Refs()
    {
        var env = new TestEnvironment();
        await using (var stream = new MemoryStream())
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new JsonArray
                {
                    new JsonObject
                    {
                        ["corpusId"] = "corpus1",
                        ["textId"] = "MAT",
                        ["refs"] = new JsonArray { "MAT 1:1" },
                        ["sourceTokens"] = new JsonArray { "sourceToken1" },
                        ["targetTokens"] = new JsonArray { "targetToken1" },
                        ["alignment"] = "0-0:1.0:1.0",
                    },
                }
            );
            stream.Seek(0, SeekOrigin.Begin);
            await env.Service.InsertInferenceResultsAsync("engine1", "build1", stream);
        }

        await env
            .PlatformService.Received()
            .InsertWordAlignmentsAsync(
                "engine1",
                Arg.Any<IAsyncEnumerable<WordAlignmentContract>>(),
                Arg.Any<CancellationToken>()
            );
        Assert.That(env.WordAlignmentContracts, Has.Count.EqualTo(1));
        Assert.That(
            env.WordAlignmentContracts[0],
            Is.EqualTo(
                    new WordAlignmentContract
                    {
                        CorpusId = "corpus1",
                        TextId = "MAT",
                        SourceRefs = [],
                        TargetRefs = ["MAT 1:1"],
                        SourceTokens = ["sourceToken1"],
                        TargetTokens = ["targetToken1"],
                        Alignment =
                        [
                            new AlignedWordPairContract
                            {
                                SourceIndex = 0,
                                TargetIndex = 0,
                                Score = 1.0,
                            },
                        ],
                    }
                )
                .UsingPropertiesComparer()
        );
    }

    [Test]
    public async Task InsertWordAlignmentsAsync_SourceAndTargetRefs()
    {
        var env = new TestEnvironment();
        await using (var stream = new MemoryStream())
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new JsonArray
                {
                    new JsonObject
                    {
                        ["corpusId"] = "corpus1",
                        ["textId"] = "MAT",
                        ["sourceRefs"] = new JsonArray { "MAT 1:1" },
                        ["targetRefs"] = new JsonArray { "MAT 1:2" },
                        ["sourceTokens"] = new JsonArray { "sourceToken1" },
                        ["targetTokens"] = new JsonArray { "targetToken1" },
                        ["alignment"] = "0-0:1.0:1.0",
                    },
                }
            );
            stream.Seek(0, SeekOrigin.Begin);
            await env.Service.InsertInferenceResultsAsync("engine1", "build1", stream);
        }

        await env
            .PlatformService.Received()
            .InsertWordAlignmentsAsync(
                "engine1",
                Arg.Any<IAsyncEnumerable<WordAlignmentContract>>(),
                Arg.Any<CancellationToken>()
            );
        Assert.That(env.WordAlignmentContracts, Has.Count.EqualTo(1));
        Assert.That(
            env.WordAlignmentContracts[0],
            Is.EqualTo(
                    new WordAlignmentContract
                    {
                        CorpusId = "corpus1",
                        TextId = "MAT",
                        SourceRefs = ["MAT 1:1"],
                        TargetRefs = ["MAT 1:2"],
                        SourceTokens = ["sourceToken1"],
                        TargetTokens = ["targetToken1"],
                        Alignment =
                        [
                            new AlignedWordPairContract
                            {
                                SourceIndex = 0,
                                TargetIndex = 0,
                                Score = 1.0,
                            },
                        ],
                    }
                )
                .UsingPropertiesComparer()
        );
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            PlatformService = Substitute.For<IWordAlignmentPlatformService>();
            PlatformService
                .InsertWordAlignmentsAsync(
                    Arg.Any<string>(),
                    Arg.Any<IAsyncEnumerable<WordAlignmentContract>>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(async ci =>
                {
                    WordAlignmentContracts.Clear();
                    await foreach (
                        WordAlignmentContract wordAlignmentContract in ci.Arg<IAsyncEnumerable<WordAlignmentContract>>()
                    )
                    {
                        WordAlignmentContracts.Add(wordAlignmentContract);
                    }
                });

            Service = new ServalWordAlignmentPlatformService(PlatformService);
        }

        public ServalWordAlignmentPlatformService Service { get; }
        public IWordAlignmentPlatformService PlatformService { get; }
        public List<WordAlignmentContract> WordAlignmentContracts { get; } = [];
    }
}
