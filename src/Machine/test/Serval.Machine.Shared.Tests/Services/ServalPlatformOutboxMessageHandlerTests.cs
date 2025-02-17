using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

[TestFixture]
public class ServalPlatformOutboxMessageHandlerTests
{
    [Test]
    public async Task HandleMessageAsync_BuildStarted()
    {
        TestEnvironment env = new();

        await env.Handler.HandleMessageAsync(
            "groupId",
            ServalTranslationPlatformOutboxConstants.BuildStarted,
            JsonSerializer.Serialize(
                new BuildStartedRequest { BuildId = "C" },
                MessageOutboxOptions.JsonSerializerOptions
            ),
            null
        );

        _ = env.Client.Received(1).BuildStartedAsync(Arg.Is<BuildStartedRequest>(x => x.BuildId == "C"));
    }

    [Test]
    public async Task HandleMessageAsync_InsertInferenceResults()
    {
        TestEnvironment env = new();

        await using (MemoryStream stream = new())
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new[]
                {
                    new Pretranslation
                    {
                        CorpusId = "corpus1",
                        TextId = "MAT",
                        Refs = ["MAT 1:1"],
                        Translation = "translation"
                    }
                },
                MessageOutboxOptions.JsonSerializerOptions
            );
            stream.Seek(0, SeekOrigin.Begin);
            await env.Handler.HandleMessageAsync(
                "engine1",
                ServalTranslationPlatformOutboxConstants.InsertPretranslations,
                "engine1",
                stream
            );
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
            Client.BuildStartedAsync(Arg.Any<BuildStartedRequest>()).Returns(CreateEmptyUnaryCall());
            Client.BuildCanceledAsync(Arg.Any<BuildCanceledRequest>()).Returns(CreateEmptyUnaryCall());
            Client.BuildFaultedAsync(Arg.Any<BuildFaultedRequest>()).Returns(CreateEmptyUnaryCall());
            Client.BuildCompletedAsync(Arg.Any<BuildCompletedRequest>()).Returns(CreateEmptyUnaryCall());
            Client
                .IncrementTrainEngineCorpusSizeAsync(Arg.Any<IncrementTrainEngineCorpusSizeRequest>())
                .Returns(CreateEmptyUnaryCall());
            PretranslationWriter = Substitute.For<IClientStreamWriter<InsertPretranslationsRequest>>();
            Client
                .InsertPretranslations(cancellationToken: Arg.Any<CancellationToken>())
                .Returns(
                    TestCalls.AsyncClientStreamingCall(
                        PretranslationWriter,
                        Task.FromResult(new Empty()),
                        Task.FromResult(new Metadata()),
                        () => Status.DefaultSuccess,
                        () => new Metadata(),
                        () => { }
                    )
                );

            Handler = new ServalTranslationPlatformOutboxMessageHandler(Client);
        }

        public TranslationPlatformApi.TranslationPlatformApiClient Client { get; }
        public ServalTranslationPlatformOutboxMessageHandler Handler { get; }
        public IClientStreamWriter<InsertPretranslationsRequest> PretranslationWriter { get; }

        private static AsyncUnaryCall<Empty> CreateEmptyUnaryCall()
        {
            return new AsyncUnaryCall<Empty>(
                Task.FromResult(new Empty()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }
            );
        }
    }
}
