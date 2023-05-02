using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.ApiServer;

[TestFixture]
[Category("Integration")]
public class TranslationEngineTests
{
    [Test]
    public async Task GetAllAsync()
    {
        using var env = new TestEnvironment();
        await env.Engines.InsertAsync(
            new Engine
            {
                Name = "test",
                SourceLanguage = "en",
                TargetLanguage = "en",
                Type = "Echo",
                Owner = "client1"
            }
        );

        ITranslationEnginesClient client = env.CreateClient();
        ICollection<TranslationEngine> results = await client.GetAllAsync();
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("test"));
    }

    [Test]
    public async Task CreateAsync()
    {
        using var env = new TestEnvironment();
        ITranslationEnginesClient client = env.CreateClient();
        TranslationEngine result = await client.CreateAsync(
            new TranslationEngineConfig
            {
                Name = "test",
                SourceLanguage = "en",
                TargetLanguage = "en",
                Type = "Echo"
            }
        );
        Assert.That(result.Name, Is.EqualTo("test"));
        Engine? engine = await env.Engines.GetAsync(result.Id);
        Assert.That(engine, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(engine.Name, Is.EqualTo("test"));
            Assert.That(engine.Owner, Is.EqualTo("client1"));
        });
    }

    [Test]
    public async Task AddCorpusAsync()
    {
        using var env = new TestEnvironment();
        var engine = new Engine
        {
            Owner = "client1",
            Name = "test",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "Echo"
        };
        await env.Engines.InsertAsync(engine);

        var srcFile = new DataFiles.Models.DataFile
        {
            Owner = "client1",
            Name = "src.txt",
            Filename = "abcd",
            Format = Shared.Contracts.FileFormat.Text
        };
        var trgFile = new DataFiles.Models.DataFile
        {
            Owner = "client1",
            Name = "trg.txt",
            Filename = "efgh",
            Format = Shared.Contracts.FileFormat.Text
        };
        await env.DataFiles.InsertAllAsync(new[] { srcFile, trgFile });

        ITranslationEnginesClient client = env.CreateClient();
        TranslationCorpus result = await client.AddCorpusAsync(
            engine.Id,
            new TranslationCorpusConfig
            {
                Name = "TestCorpus",
                SourceLanguage = "en",
                TargetLanguage = "en",
                SourceFiles =
                {
                    new TranslationCorpusFileConfig { FileId = srcFile.Id, TextId = "all" }
                },
                TargetFiles =
                {
                    new TranslationCorpusFileConfig { FileId = trgFile.Id, TextId = "all" }
                }
            }
        );
        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("TestCorpus"));
            Assert.That(result.SourceFiles.First().File.Id, Is.EqualTo(srcFile.Id));
            Assert.That(result.TargetFiles.First().File.Id, Is.EqualTo(trgFile.Id));
        });
        engine = await env.Engines.GetAsync(engine.Id);
        Assert.That(engine, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(engine.Corpora[0].SourceFiles[0].Filename, Is.EqualTo("abcd"));
            Assert.That(engine.Corpora[0].TargetFiles[0].Filename, Is.EqualTo("efgh"));
        });
    }

    [Test]
    public async Task TranslateAsync()
    {
        using var env = new TestEnvironment();
        var engine = new Engine
        {
            Name = "test",
            SourceLanguage = "en",
            TargetLanguage = "en",
            Type = "Echo",
            Owner = "client1"
        };
        await env.Engines.InsertAsync(engine);

        ITranslationEnginesClient client = env.CreateClient();
        Client.TranslationResult result = await client.TranslateAsync(engine.Id, "This is a test .");
        Assert.That(result.Translation, Is.EqualTo("This is a test ."));
        Assert.That(result.Sources, Has.Count.EqualTo(5));
        Assert.That(
            result.Sources,
            Has.All.EquivalentTo(new[] { Client.TranslationSource.Primary, Client.TranslationSource.Secondary })
        );
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IServiceScope _scope;
        private readonly IMongoClient _mongoClient;

        public TestEnvironment()
        {
            _mongoClient = new MongoClient();
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            Engines = _scope.ServiceProvider.GetRequiredService<IRepository<Engine>>();
            DataFiles = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.DataFile>>();
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }

        public ITranslationEnginesClient CreateClient(IEnumerable<string>? scope = null)
        {
            if (scope is null)
            {
                scope = new[]
                {
                    Scopes.CreateTranslationEngines,
                    Scopes.ReadTranslationEngines,
                    Scopes.UpdateTranslationEngines,
                    Scopes.DeleteTranslationEngines
                };
            }
            var httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        var client = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
                        client
                            .CreateAsync(Arg.Any<CreateRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(new Empty()));
                        var translationResult = new Translation.V1.TranslationResult
                        {
                            Translation = "This is a test .",
                            SourceTokens = { "This is a test .".Split() },
                            TargetTokens = { "This is a test .".Split() },
                            Confidences = { 1.0, 1.0, 1.0, 1.0, 1.0 },
                            Sources =
                            {
                                new TranslationSources
                                {
                                    Values =
                                    {
                                        Translation.V1.TranslationSource.Primary,
                                        Translation.V1.TranslationSource.Secondary
                                    }
                                },
                                new TranslationSources
                                {
                                    Values =
                                    {
                                        Translation.V1.TranslationSource.Primary,
                                        Translation.V1.TranslationSource.Secondary
                                    }
                                },
                                new TranslationSources
                                {
                                    Values =
                                    {
                                        Translation.V1.TranslationSource.Primary,
                                        Translation.V1.TranslationSource.Secondary
                                    }
                                },
                                new TranslationSources
                                {
                                    Values =
                                    {
                                        Translation.V1.TranslationSource.Primary,
                                        Translation.V1.TranslationSource.Secondary
                                    }
                                },
                                new TranslationSources
                                {
                                    Values =
                                    {
                                        Translation.V1.TranslationSource.Primary,
                                        Translation.V1.TranslationSource.Secondary
                                    }
                                }
                            },
                            Alignment =
                            {
                                new Translation.V1.AlignedWordPair { SourceIndex = 0, TargetIndex = 0 },
                                new Translation.V1.AlignedWordPair { SourceIndex = 1, TargetIndex = 1 },
                                new Translation.V1.AlignedWordPair { SourceIndex = 2, TargetIndex = 2 },
                                new Translation.V1.AlignedWordPair { SourceIndex = 3, TargetIndex = 3 },
                                new Translation.V1.AlignedWordPair { SourceIndex = 4, TargetIndex = 4 }
                            },
                            Phrases =
                            {
                                new Translation.V1.Phrase
                                {
                                    SourceSegmentStart = 0,
                                    SourceSegmentEnd = 5,
                                    TargetSegmentCut = 5
                                }
                            }
                        };
                        var translateResponse = new TranslateResponse { Results = { translationResult } };
                        client
                            .TranslateAsync(Arg.Any<TranslateRequest>(), null, null, Arg.Any<CancellationToken>())
                            .Returns(CreateAsyncUnaryCall(translateResponse));
                        var grpcClientFactory = Substitute.For<GrpcClientFactory>();
                        grpcClientFactory
                            .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Echo")
                            .Returns(client);
                        services.AddSingleton(grpcClientFactory);
                    });
                })
                .CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new TranslationEnginesClient(httpClient);
        }

        public void ResetDatabases()
        {
            _mongoClient.DropDatabase("serval_test");
            _mongoClient.DropDatabase("serval_test_jobs");
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

        protected override void DisposeManagedResources()
        {
            _scope.Dispose();
            Factory.Dispose();
            ResetDatabases();
        }
    }
}
