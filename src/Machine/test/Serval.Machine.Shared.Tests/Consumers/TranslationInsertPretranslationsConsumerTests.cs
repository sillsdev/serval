using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

[TestFixture]
public class TranslationInsertPretranslationsConsumerTests
{
    [Test]
    public async Task HandleMessageAsync_Refs()
    {
        TestEnvironment env = new();

        await using (MemoryStream stream = new())
        {
            var obj = new JsonObject();
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
            await env.Consumer.HandleMessageAsync("engine1", stream);
        }

        _ = env.Client.Received(1).InsertPretranslations();
        _ = env
            .PretranslationWriter.Received(1)
            .WriteAsync(
                new InsertPretranslationsRequest
                {
                    EngineId = "engine1",
                    CorpusId = "corpus1",
                    TextId = "MAT",
                    SourceRefs = { },
                    TargetRefs = { "MAT 1:1" },
                    Translation = "translation",
                    Confidence = 0.5,
                },
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task HandleMessageAsync_SourceAndTargetRefs()
    {
        TestEnvironment env = new();

        await using (MemoryStream stream = new())
        {
            var obj = new JsonObject();
            await JsonSerializer.SerializeAsync(
                stream,
                new JsonArray
                {
                    new JsonObject
                    {
                        ["corpusId"] = "corpus1",
                        ["textId"] = "MAT",
                        ["sourceRefs"] = new JsonArray { "MAT 1:1" },
                        ["targetRefs"] = new JsonArray { "MAT 1:1" },
                        ["sourceTokens"] = new JsonArray { "translation" },
                        ["translationTokens"] = new JsonArray { "translation" },
                        ["translation"] = "translation",
                        ["alignment"] = "0-0",
                    },
                }
            );

            stream.Seek(0, SeekOrigin.Begin);
            await env.Consumer.HandleMessageAsync("engine1", stream);
        }

        _ = env.Client.Received(1).InsertPretranslations();
        _ = env
            .PretranslationWriter.Received(1)
            .WriteAsync(
                new InsertPretranslationsRequest
                {
                    EngineId = "engine1",
                    CorpusId = "corpus1",
                    TextId = "MAT",
                    SourceRefs = { "MAT 1:1" },
                    TargetRefs = { "MAT 1:1" },
                    Translation = "translation",
                    SourceTokens = { "translation" },
                    TranslationTokens = { "translation" },
                    Alignment =
                    {
                        new Translation.V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                    },
                    Confidence = 0.0,
                },
                Arg.Any<CancellationToken>()
            );
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Client = Substitute.For<TranslationPlatformApi.TranslationPlatformApiClient>();
            PretranslationWriter = Substitute.For<IClientStreamWriter<InsertPretranslationsRequest>>();
            Client
                .InsertPretranslations(cancellationToken: Arg.Any<CancellationToken>())
                .Returns(
                    TestCalls.AsyncClientStreamingCall(
                        PretranslationWriter,
                        Task.FromResult(new Empty()),
                        Task.FromResult(new Metadata()),
                        () => Status.DefaultSuccess,
                        () => [],
                        () => { }
                    )
                );

            Consumer = new TranslationInsertPretranslationsConsumer(Client);
        }

        public TranslationPlatformApi.TranslationPlatformApiClient Client { get; }
        public TranslationInsertPretranslationsConsumer Consumer { get; }
        public IClientStreamWriter<InsertPretranslationsRequest> PretranslationWriter { get; }
    }
}
