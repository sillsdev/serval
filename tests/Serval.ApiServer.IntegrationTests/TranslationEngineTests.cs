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
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<Engine> Engines { get; }

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
