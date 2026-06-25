namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ServalTranslationPlatformServiceTests
{
    [Test]
    public async Task InsertInferenceResultsAsync_Refs()
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
                        ["translation"] = "translation",
                        ["sequenceConfidence"] = 0.5,
                    },
                }
            );
            stream.Seek(0, SeekOrigin.Begin);
            await env.Service.InsertInferenceResultsAsync("engine1", "build1", stream);
        }

        await env
            .PlatformService.Received()
            .InsertPretranslationsAsync(
                "engine1",
                "build1",
                Arg.Any<IAsyncEnumerable<PretranslationContract>>(),
                Arg.Any<CancellationToken>()
            );
        Assert.That(env.PretranslationContracts, Has.Count.EqualTo(1));
        Assert.That(
            env.PretranslationContracts[0],
            Is.EqualTo(
                    new PretranslationContract
                    {
                        CorpusId = "corpus1",
                        TextId = "MAT",
                        SourceRefs = [],
                        TargetRefs = ["MAT 1:1"],
                        Translation = "translation",
                        SourceTokens = [],
                        TranslationTokens = [],
                        Alignment = [],
                        Confidence = 0.5,
                    }
                )
                .UsingPropertiesComparer()
        );
    }

    [Test]
    public async Task InsertInferenceResultsAsync_SourceAndTargetRefs()
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
                        ["translationTokens"] = new JsonArray { "translationToken1" },
                        ["translation"] = "translation",
                        ["alignment"] = "0-0",
                    },
                }
            );
            stream.Seek(0, SeekOrigin.Begin);
            await env.Service.InsertInferenceResultsAsync("engine1", "build1", stream);
        }

        await env
            .PlatformService.Received()
            .InsertPretranslationsAsync(
                "engine1",
                "build1",
                Arg.Any<IAsyncEnumerable<PretranslationContract>>(),
                Arg.Any<CancellationToken>()
            );
        Assert.That(env.PretranslationContracts, Has.Count.EqualTo(1));
        Assert.That(
            env.PretranslationContracts[0],
            Is.EqualTo(
                    new PretranslationContract
                    {
                        CorpusId = "corpus1",
                        TextId = "MAT",
                        SourceRefs = ["MAT 1:1"],
                        TargetRefs = ["MAT 1:2"],
                        Translation = "translation",
                        SourceTokens = ["sourceToken1"],
                        TranslationTokens = ["translationToken1"],
                        Alignment =
                        [
                            new AlignedWordPairContract
                            {
                                SourceIndex = 0,
                                TargetIndex = 0,
                                Score = -1,
                            },
                        ],
                        Confidence = 0.0,
                    }
                )
                .UsingPropertiesComparer()
        );
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            PlatformService = Substitute.For<ITranslationPlatformService>();
            PlatformService
                .InsertPretranslationsAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IAsyncEnumerable<PretranslationContract>>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(async ci =>
                {
                    PretranslationContracts.Clear();
                    await foreach (
                        PretranslationContract pretranslationContract in ci.Arg<
                            IAsyncEnumerable<PretranslationContract>
                        >()
                    )
                    {
                        PretranslationContracts.Add(pretranslationContract);
                    }
                });

            Service = new ServalTranslationPlatformService(PlatformService);
        }

        public ServalTranslationPlatformService Service { get; }
        public ITranslationPlatformService PlatformService { get; }
        public List<PretranslationContract> PretranslationContracts { get; } = [];
    }
}
