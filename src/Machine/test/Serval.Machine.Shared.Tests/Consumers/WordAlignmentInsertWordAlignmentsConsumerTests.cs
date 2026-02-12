using Google.Protobuf.WellKnownTypes;
using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

[TestFixture]
public class WordAlignmentInsertWordAlignmentsConsumerTests
{
    [Test]
    public async Task HandleMessageAsync_Refs()
    {
        TestEnvironment env = new();

        await using (MemoryStream stream = new())
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new JsonArray
                {
                    new JsonObject
                    {
                        { "corpusId", "corpus1" },
                        { "textId", "MAT" },
                        {
                            "refs",
                            new JsonArray { "MAT 1:1" }
                        },
                        {
                            "sourceTokens",
                            new JsonArray { "sourceToken1" }
                        },
                        {
                            "targetTokens",
                            new JsonArray { "targetToken1" }
                        },
                        { "alignment", "0-0:1.0:1.0" },
                    },
                }
            );
            stream.Seek(0, SeekOrigin.Begin);
            await env.Handler.HandleMessageAsync("engine1", stream);
        }

        _ = env.Client.Received(1).InsertWordAlignments();
        _ = env
            .WordAlignmentsWriter.Received(1)
            .WriteAsync(
                new InsertWordAlignmentsRequest
                {
                    EngineId = "engine1",
                    CorpusId = "corpus1",
                    TextId = "MAT",
                    SourceRefs = { },
                    TargetRefs = { "MAT 1:1" },
                    SourceTokens = { "sourceToken1" },
                    TargetTokens = { "targetToken1" },
                    Alignment =
                    {
                        new WordAlignment.V1.AlignedWordPair()
                        {
                            SourceIndex = 0,
                            TargetIndex = 0,
                            Score = 1.0,
                        },
                    },
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
            await JsonSerializer.SerializeAsync(
                stream,
                new JsonArray
                {
                    new JsonObject
                    {
                        { "corpusId", "corpus1" },
                        { "textId", "MAT" },
                        {
                            "sourceRefs",
                            new JsonArray { "MAT 1:1" }
                        },
                        {
                            "targetRefs",
                            new JsonArray { "MAT 1:1" }
                        },
                        {
                            "sourceTokens",
                            new JsonArray { "sourceToken1" }
                        },
                        {
                            "targetTokens",
                            new JsonArray { "targetToken1" }
                        },
                        { "alignment", "0-0:1.0:1.0" },
                    },
                }
            );
            stream.Seek(0, SeekOrigin.Begin);
            await env.Handler.HandleMessageAsync("engine1", stream);
        }

        _ = env.Client.Received(1).InsertWordAlignments();
        _ = env
            .WordAlignmentsWriter.Received(1)
            .WriteAsync(
                new InsertWordAlignmentsRequest
                {
                    EngineId = "engine1",
                    CorpusId = "corpus1",
                    TextId = "MAT",
                    SourceRefs = { "MAT 1:1" },
                    TargetRefs = { "MAT 1:1" },
                    SourceTokens = { "sourceToken1" },
                    TargetTokens = { "targetToken1" },
                    Alignment =
                    {
                        new WordAlignment.V1.AlignedWordPair()
                        {
                            SourceIndex = 0,
                            TargetIndex = 0,
                            Score = 1.0,
                        },
                    },
                },
                Arg.Any<CancellationToken>()
            );
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Client = Substitute.For<WordAlignmentPlatformApi.WordAlignmentPlatformApiClient>();
            WordAlignmentsWriter = Substitute.For<IClientStreamWriter<InsertWordAlignmentsRequest>>();
            Client
                .InsertWordAlignments(cancellationToken: Arg.Any<CancellationToken>())
                .Returns(
                    TestCalls.AsyncClientStreamingCall(
                        WordAlignmentsWriter,
                        Task.FromResult(new Empty()),
                        Task.FromResult(new Metadata()),
                        () => Status.DefaultSuccess,
                        () => [],
                        () => { }
                    )
                );

            Handler = new WordAlignmentInsertWordAlignmentsConsumer(Client);
        }

        public WordAlignmentPlatformApi.WordAlignmentPlatformApiClient Client { get; }
        public WordAlignmentInsertWordAlignmentsConsumer Handler { get; }
        public IClientStreamWriter<InsertWordAlignmentsRequest> WordAlignmentsWriter { get; }
    }
}
