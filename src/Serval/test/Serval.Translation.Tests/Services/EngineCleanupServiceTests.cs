using Google.Protobuf.WellKnownTypes;
using MassTransit.Mediator;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

[TestFixture]
public class EngineCleanupServiceTests
{
    [Test]
    public async Task CleanupAsync()
    {
        TestEnvironment env = new();
        Assert.That(env.Engines.Count, Is.EqualTo(2));
        await env.CheckEnginesAsync();
        Assert.That(env.Engines.Count, Is.EqualTo(1));
        Assert.That((await env.Engines.GetAllAsync())[0].Id, Is.EqualTo("engine2"));
    }

    private class TestEnvironment
    {
        public MemoryRepository<Engine> Engines { get; }

        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            Engines.Add(
                new Engine
                {
                    Id = "engine1",
                    SourceLanguage = "en",
                    TargetLanguage = "es",
                    Type = "Nmt",
                    Owner = "client1",
                    IsInitialized = false,
                    DateCreated = DateTime.UtcNow.Subtract(TimeSpan.FromHours(10))
                }
            );
            Engines.Add(
                new Engine
                {
                    Id = "engine2",
                    SourceLanguage = "en",
                    TargetLanguage = "es",
                    Type = "Nmt",
                    Owner = "client1",
                    IsInitialized = true,
                    DateCreated = DateTime.UtcNow.Subtract(TimeSpan.FromHours(10))
                }
            );

            Service = new EngineCleanupService(
                Substitute.For<IServiceProvider>(),
                Substitute.For<ILogger<EngineCleanupService>>(),
                TimeSpan.Zero
            );

            var translationServiceClient = Substitute.For<TranslationEngineApi.TranslationEngineApiClient>();
            translationServiceClient.DeleteAsync(Arg.Any<DeleteRequest>()).Returns(CreateAsyncUnaryCall(new Empty()));

            GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
            grpcClientFactory
                .CreateClient<TranslationEngineApi.TranslationEngineApiClient>("Nmt")
                .Returns(translationServiceClient);

            var engineService = new EngineService(
                Engines,
                new MemoryRepository<Build>(),
                new MemoryRepository<Pretranslation>(),
                Substitute.For<IScopedMediator>(),
                grpcClientFactory,
                Substitute.For<IOptionsMonitor<DataFileOptions>>(),
                new MemoryDataAccessContext(),
                new LoggerFactory(),
                Substitute.For<IScriptureDataFileService>()
            );

            Service.EngineService = engineService;
        }

        public EngineCleanupService Service { get; }

        public async Task CheckEnginesAsync()
        {
            await Service.CheckEntitiesAsync(Engines, CancellationToken.None);
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
