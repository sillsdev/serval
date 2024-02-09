using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Serval.Shared.Utils;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

[TestFixture]
public class EngineServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public void TranslateAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.TranslateAsync("engine1", "esto es una prueba."));
    }

    [Test]
    public async Task TranslateAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineAsync()).Id;
        Models.TranslationResult? result = await env.Service.TranslateAsync(engineId, "esto es una prueba.");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Translation, Is.EqualTo("this is a test."));
    }

    [Test]
    public void GetWordGraphAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(
            () => env.Service.GetWordGraphAsync("engine1", "esto es una prueba.")
        );
    }

    [Test]
    public async Task GetWordGraphAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineAsync()).Id;
        Models.WordGraph? result = await env.Service.GetWordGraphAsync(engineId, "esto es una prueba.");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Arcs.SelectMany(a => a.TargetTokens), Is.EqualTo("this is a test .".Split()));
    }

    [Test]
    public void TrainSegmentAsync_EngineDoesNotExist()
    {
        var env = new TestEnvironment();
        Assert.ThrowsAsync<EntityNotFoundException>(
            () => env.Service.TrainSegmentPairAsync("engine1", "esto es una prueba.", "this is a test.", true)
        );
    }

    [Test]
    public async Task TrainSegmentAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineAsync()).Id;
        Assert.DoesNotThrowAsync(
            () => env.Service.TrainSegmentPairAsync(engineId, "esto es una prueba.", "this is a test.", true)
        );
    }

    [Test]
    public async Task CreateAsync()
    {
        var env = new TestEnvironment();
        var engine = new Engine
        {
            Id = "engine1",
            SourceLanguage = "es",
            TargetLanguage = "en",
            Type = "Smt"
        };
        await env.Service.CreateAsync(engine);

        engine = (await env.Engines.GetAsync("engine1"))!;
        Assert.That(engine.SourceLanguage, Is.EqualTo("es"));
        Assert.That(engine.TargetLanguage, Is.EqualTo("en"));
    }

    [Test]
    public async Task DeleteAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineAsync()).Id;
        await env.Service.DeleteAsync("engine1");
        Engine? engine = await env.Engines.GetAsync(engineId);
        Assert.That(engine, Is.Null);
    }

    [Test]
    public async Task DeleteAsync_ProjectDoesNotExist()
    {
        var env = new TestEnvironment();
        await env.CreateEngineAsync();
        Assert.ThrowsAsync<EntityNotFoundException>(() => env.Service.DeleteAsync("engine3"));
    }

    [Test]
    public async Task StartBuildAsync_EngineExists()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineAsync()).Id;
        Assert.DoesNotThrowAsync(() => env.Service.StartBuildAsync(new Build { Id = BUILD1_ID, EngineRef = engineId }));
    }

    [Test]
    public async Task CancelBuildAsync_EngineExistsNotBuilding()
    {
        var env = new TestEnvironment();
        string engineId = (await env.CreateEngineAsync()).Id;
        await env.Service.CancelBuildAsync(engineId);
    }

    [Test]
    public async Task UpdateCorpusAsync()
    {
        var env = new TestEnvironment();
        Engine engine = await env.CreateEngineAsync();
        string corpusId = engine.Corpora.First().Id;

        Models.Corpus? corpus = await env.Service.UpdateCorpusAsync(
            engine.Id,
            corpusId,
            sourceFiles: new[]
            {
                new Models.CorpusFile
                {
                    Id = "file1",
                    Filename = "file1.txt",
                    Format = Shared.Contracts.FileFormat.Text,
                    TextId = "text1"
                },
                new Models.CorpusFile
                {
                    Id = "file3",
                    Filename = "file3.txt",
                    Format = Shared.Contracts.FileFormat.Text,
                    TextId = "text2"
                },
            },
            null
        );

        Assert.That(corpus, Is.Not.Null);
        Assert.That(corpus!.SourceFiles.Count, Is.EqualTo(2));
        Assert.That(corpus.SourceFiles[0].Id, Is.EqualTo("file1"));
        Assert.That(corpus.SourceFiles[1].Id, Is.EqualTo("file3"));
        Assert.That(corpus.TargetFiles.Count, Is.EqualTo(1));
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            var translationServiceClient = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
            var translationResult = new V1.TranslationResult
            {
                Translation = "this is a test.",
                SourceTokens = { "esto es una prueba .".Split() },
                TargetTokens = { "this is a test .".Split() },
                Confidences = { 1.0, 1.0, 1.0, 1.0, 1.0 },
                Sources =
                {
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } },
                    new TranslationSources { Values = { V1.TranslationSource.Primary } }
                },
                Alignment =
                {
                    new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                    new V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                    new V1.AlignedWordPair { SourceIndex = 2, TargetIndex = 2 },
                    new V1.AlignedWordPair { SourceIndex = 3, TargetIndex = 3 },
                    new V1.AlignedWordPair { SourceIndex = 4, TargetIndex = 4 }
                },
                Phrases =
                {
                    new V1.Phrase
                    {
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 5,
                        TargetSegmentCut = 5
                    }
                }
            };
            var translateResponse = new TranslateResponse { Results = { translationResult } };
            translationServiceClient
                .TranslateAsync(Arg.Any<TranslateRequest>())
                .Returns(CreateAsyncUnaryCall(translateResponse));
            var wordGraph = new V1.WordGraph
            {
                SourceTokens = { "esto es una prueba .".Split() },
                FinalStates = { 3 },
                Arcs =
                {
                    new V1.WordGraphArc
                    {
                        PrevState = 0,
                        NextState = 1,
                        Score = 1.0,
                        TargetTokens = { "this is".Split() },
                        Alignment =
                        {
                            new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                            new V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 }
                        },
                        SourceSegmentStart = 0,
                        SourceSegmentEnd = 2,
                        Sources = { GetSources(2, false) },
                        Confidences = { 1.0, 1.0 }
                    },
                    new V1.WordGraphArc
                    {
                        PrevState = 1,
                        NextState = 2,
                        Score = 1.0,
                        TargetTokens = { "a test".Split() },
                        Alignment =
                        {
                            new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                            new V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 }
                        },
                        SourceSegmentStart = 2,
                        SourceSegmentEnd = 4,
                        Sources = { GetSources(2, false) },
                        Confidences = { 1.0, 1.0 }
                    },
                    new V1.WordGraphArc
                    {
                        PrevState = 2,
                        NextState = 3,
                        Score = 1.0,
                        TargetTokens = { ".".Split() },
                        Alignment =
                        {
                            new V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 }
                        },
                        SourceSegmentStart = 4,
                        SourceSegmentEnd = 5,
                        Sources = { GetSources(1, false) },
                        Confidences = { 1.0 }
                    }
                }
            };
            var getWordGraphResponse = new GetWordGraphResponse { WordGraph = wordGraph };
            translationServiceClient
                .GetWordGraphAsync(Arg.Any<GetWordGraphRequest>())
                .Returns(CreateAsyncUnaryCall(getWordGraphResponse));
            translationServiceClient
                .CancelBuildAsync(Arg.Any<CancelBuildRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            translationServiceClient
                .CreateAsync(Arg.Any<CreateRequest>())
                .Returns(CreateAsyncUnaryCall(new CreateResponse()));
            translationServiceClient.DeleteAsync(Arg.Any<DeleteRequest>()).Returns(CreateAsyncUnaryCall(new Empty()));
            translationServiceClient
                .StartBuildAsync(Arg.Any<StartBuildRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            translationServiceClient
                .TrainSegmentPairAsync(Arg.Any<TrainSegmentPairRequest>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            var grpcClientFactory = Substitute.For<GrpcClientFactory>();
            grpcClientFactory
                .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Smt")
                .Returns(translationServiceClient);
            var dataFileOptions = Substitute.For<IOptionsMonitor<DataFileOptions>>();
            dataFileOptions.CurrentValue.Returns(new DataFileOptions());

            Service = new EngineService(
                Engines,
                new MemoryRepository<Build>(),
                new MemoryRepository<Pretranslation>(),
                grpcClientFactory,
                dataFileOptions,
                new MemoryDataAccessContext(),
                new LoggerFactory(),
                new ScriptureDataFileService(new FileSystem(), dataFileOptions)
            );
        }

        public EngineService Service { get; }
        public IRepository<Engine> Engines { get; }

        public async Task<Engine> CreateEngineAsync()
        {
            var engine = new Engine
            {
                Id = "engine1",
                SourceLanguage = "es",
                TargetLanguage = "en",
                Type = "Smt",
                Corpora = new List<Models.Corpus>
                {
                    new Models.Corpus
                    {
                        Id = "corpus1",
                        SourceLanguage = "es",
                        TargetLanguage = "en",
                        SourceFiles = new List<Models.CorpusFile>
                        {
                            new Models.CorpusFile
                            {
                                Id = "file1",
                                Filename = "file1.txt",
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
                        },
                        TargetFiles = new List<Models.CorpusFile>
                        {
                            new Models.CorpusFile
                            {
                                Id = "file2",
                                Filename = "file2.txt",
                                Format = Shared.Contracts.FileFormat.Text,
                                TextId = "text1"
                            }
                        },
                    }
                }
            };
            await Engines.InsertAsync(engine);
            return engine;
        }

        private static IEnumerable<TranslationSources> GetSources(int count, bool isUnknown)
        {
            var sources = new TranslationSources[count];
            for (int i = 0; i < count; i++)
            {
                sources[i] = new TranslationSources();
                if (!isUnknown)
                    sources[i].Values.Add(TranslationSource.Primary);
            }
            return sources;
        }

        private static AsyncUnaryCall<TResponse> CreateAsyncUnaryCall<TResponse>(TResponse response)
        {
            return new AsyncUnaryCall<TResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }
            );
        }
    }
}
