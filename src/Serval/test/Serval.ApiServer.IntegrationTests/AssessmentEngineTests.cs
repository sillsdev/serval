using Google.Protobuf.WellKnownTypes;
using Serval.Assessment.Models;
using Serval.Assessment.V1;
using static Serval.ApiServer.Utils;

namespace Serval.ApiServer;

[TestFixture]
public class AssessmentEngineTests
{
    private const string EngineType = "Test";
    private const string ClientId1 = "client1";

    [Test]
    public async Task CreateAsync()
    {
        using TestEnvironment env = new();
        DataFiles.Models.DataFile dataFile = await env.AddDataFileAsync();

        AssessmentEnginesClient client = env.CreateClient();
        AssessmentEngine result = await client.CreateAsync(
            new()
            {
                Name = "test",
                Type = EngineType,
                Corpus = new() { Language = "en", Files = { new() { FileId = dataFile.Id } } }
            }
        );
        Assert.That(result.Name, Is.EqualTo("test"));
        AssessmentEngine? engine = await client.GetAsync(result.Id);
        Assert.That(engine, Is.Not.Null);
        Assert.That(engine.Name, Is.EqualTo("test"));
    }

    [Test]
    public async Task StartJobAsync()
    {
        using TestEnvironment env = new();
        Engine engine = await env.AddEngineAsync();

        AssessmentEnginesClient client = env.CreateClient();
        AssessmentJob result = await client.StartJobAsync(engine.Id, new() { Name = "test" });
        Assert.That(result.Name, Is.EqualTo("test"));
        AssessmentJob? job = await client.GetJobAsync(engine.Id, result.Id);
        Assert.That(job, Is.Not.Null);
        Assert.That(job.Name, Is.EqualTo("test"));
    }

    [Test]
    public async Task GetAllResultsAsync()
    {
        using TestEnvironment env = new();
        Job job = await env.AddJobAsync();
        await env.Results.InsertAllAsync(
            [
                new()
                {
                    EngineRef = job.EngineRef,
                    JobRef = job.Id,
                    TextId = "text1",
                    Ref = "1"
                },
                new()
                {
                    EngineRef = job.EngineRef,
                    JobRef = job.Id,
                    TextId = "text2",
                    Ref = "2"
                }
            ]
        );

        AssessmentEnginesClient client = env.CreateClient();

        IList<AssessmentResult> results = await client.GetAllResultsAsync(job.EngineRef, job.Id);
        Assert.That(results, Has.Count.EqualTo(2));

        results = await client.GetAllResultsAsync(job.EngineRef, job.Id, textId: "text1");
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Ref, Is.EqualTo("1"));
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IServiceScope _scope;
        private readonly MongoClient _mongoClient;

        public TestEnvironment()
        {
            MongoClientSettings clientSettings = new() { LinqProvider = LinqProvider.V2 };
            _mongoClient = new MongoClient(clientSettings);
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            Engines = _scope.ServiceProvider.GetRequiredService<IRepository<Engine>>();
            DataFiles = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.DataFile>>();
            Results = _scope.ServiceProvider.GetRequiredService<IRepository<Result>>();
            Jobs = _scope.ServiceProvider.GetRequiredService<IRepository<Job>>();

            Client = Substitute.For<AssessmentEngineApi.AssessmentEngineApiClient>();
            Client
                .CreateAsync(Arg.Any<CreateRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            Client
                .DeleteAsync(Arg.Any<DeleteRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            Client
                .StartJobAsync(Arg.Any<StartJobRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            Client
                .CancelJobAsync(Arg.Any<CancelJobRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
            Client
                .DeleteAsync(Arg.Any<DeleteRequest>(), null, null, Arg.Any<CancellationToken>())
                .Returns(CreateAsyncUnaryCall(new Empty()));
        }

        public ServalWebApplicationFactory Factory { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }
        public IRepository<Result> Results { get; }
        public IRepository<Job> Jobs { get; }
        public AssessmentEngineApi.AssessmentEngineApiClient Client { get; }

        public AssessmentEnginesClient CreateClient(IEnumerable<string>? scope = null)
        {
            scope ??=
            [
                Scopes.CreateAssessmentEngines,
                Scopes.ReadAssessmentEngines,
                Scopes.UpdateAssessmentEngines,
                Scopes.DeleteAssessmentEngines
            ];
            HttpClient httpClient = Factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        GrpcClientFactory grpcClientFactory = Substitute.For<GrpcClientFactory>();
                        grpcClientFactory
                            .CreateClient<AssessmentEngineApi.AssessmentEngineApiClient>(EngineType)
                            .Returns(Client);
                        services.AddSingleton(grpcClientFactory);
                    });
                })
                .CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new AssessmentEnginesClient(httpClient);
        }

        public async Task<DataFiles.Models.DataFile> AddDataFileAsync()
        {
            DataFiles.Models.DataFile dataFile =
                new()
                {
                    Owner = ClientId1,
                    Format = Shared.Contracts.FileFormat.Paratext,
                    Id = "f00000000000000000000001",
                    Name = "file1.zip",
                    Filename = "file1.zip"
                };
            await DataFiles.InsertAsync(dataFile);
            return dataFile;
        }

        public async Task<Engine> AddEngineAsync()
        {
            DataFiles.Models.DataFile dataFile = await AddDataFileAsync();
            Engine engine =
                new()
                {
                    Owner = ClientId1,
                    Type = EngineType,
                    Corpus = new()
                    {
                        Language = "en",
                        Files =
                        [
                            new()
                            {
                                Id = dataFile.Id,
                                Format = Shared.Contracts.FileFormat.Paratext,
                                Filename = dataFile.Filename,
                                TextId = "all"
                            }
                        ]
                    },
                };
            await Engines.InsertAsync(engine);
            return engine;
        }

        public async Task<Job> AddJobAsync()
        {
            Engine engine = await AddEngineAsync();
            Job job =
                new()
                {
                    Name = "test",
                    EngineRef = engine.Id,
                    State = Shared.Contracts.JobState.Completed,
                    Message = "Completed",
                    DateFinished = DateTime.UtcNow
                };
            await Jobs.InsertAsync(job);
            return job;
        }

        public void ResetDatabases()
        {
            _mongoClient.DropDatabase("serval_test");
            _mongoClient.DropDatabase("serval_test_jobs");
        }

        protected override void DisposeManagedResources()
        {
            _scope.Dispose();
            Factory.Dispose();
            ResetDatabases();
        }
    }
}
