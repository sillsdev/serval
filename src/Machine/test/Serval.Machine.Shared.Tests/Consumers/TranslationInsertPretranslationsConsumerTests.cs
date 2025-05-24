using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

[TestFixture]
public class TranslationInsertPretranslationsConsumerTests
{
    [Test]
    public async Task HandleMessageAsync()
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
                        ["translation"] = "translation"
                    }
                }
            );

            stream.Seek(0, SeekOrigin.Begin);
            await env.Consumer.HandleMessageAsync("engine1", stream);
        }

        _ = env.Client.Received(1).InsertPretranslations();
        _ = env.PretranslationWriter.Received(1)
            .WriteAsync(
                new InsertPretranslationsRequest
                {
                    EngineId = "engine1",
                    CorpusId = "corpus1",
                    TextId = "MAT",
                    Refs = { "MAT 1:1" },
                    Translation = "translation"
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
