using System.Globalization;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

[TestFixture]
public class PlatformServiceTests
{
    [Test]
    public async Task TestBuildStateTransitionsAsync()
    {
        var env = new TestEnvironment();
        await env.Engines.InsertAsync(
            new Engine()
            {
                Id = "e0",
                Owner = "owner1",
                Type = "nmt",
                SourceLanguage = "en",
                TargetLanguage = "es",
                Corpora = []
            }
        );
        await env.Builds.InsertAsync(new Build() { Id = "b0", EngineRef = "e0" });
        await env.PlatformService.BuildStarted(new BuildStartedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.JobState.Active));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.True);

        await env.PlatformService.BuildCanceled(new BuildCanceledRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.JobState.Canceled));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.BuildRestarting(
            new BuildRestartingRequest() { BuildId = "b0" },
            env.ServerCallContext
        );
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.JobState.Pending));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        Assert.That(env.Pretranslations.Count, Is.EqualTo(0));
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));

        await env.PlatformService.BuildFaulted(new BuildFaultedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(0));
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.JobState.Faulted));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.BuildRestarting(
            new BuildRestartingRequest() { BuildId = "b0" },
            env.ServerCallContext
        );
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
        await env.PlatformService.BuildCompleted(new BuildCompletedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
        await env.PlatformService.BuildStarted(new BuildStartedRequest() { BuildId = "b0" }, env.ServerCallContext);
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        await env.PlatformService.BuildCompleted(new BuildCompletedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateBuildStatusAsync()
    {
        var env = new TestEnvironment();
        await env.Engines.InsertAsync(
            new Engine()
            {
                Id = "e0",
                Owner = "owner1",
                Type = "nmt",
                SourceLanguage = "en",
                TargetLanguage = "es",
                Corpora = []
            }
        );
        await env.Builds.InsertAsync(new Build() { Id = "b0", EngineRef = "e0" });
        Assert.That(env.Builds.Get("b0").QueueDepth, Is.Null);
        Assert.That(env.Builds.Get("b0").PercentCompleted, Is.Null);
        await env.PlatformService.UpdateBuildStatus(
            new UpdateBuildStatusRequest()
            {
                BuildId = "b0",
                QueueDepth = 1,
                PercentCompleted = 0.5
            },
            env.ServerCallContext
        );
        Assert.That(env.Builds.Get("b0").QueueDepth, Is.EqualTo(1));
        Assert.That(env.Builds.Get("b0").PercentCompleted, Is.EqualTo(0.5));
    }

    [Test]
    public async Task UpdateBuildExecutionData()
    {
        var env = new TestEnvironment();

        var engine = new Engine()
        {
            Id = "e0",
            Owner = "owner1",
            Type = "nmt",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Corpora = []
        };
        await env.Engines.InsertAsync(engine);

        // Add build to mongo DB with some execution data, some fields that will not be overwritten and some that will
        var build = new Build()
        {
            Id = "123",
            EngineRef = "e0",
            ExecutionData = new Dictionary<string, string>
            {
                { "trainCount", "0" },
                { "pretransalteCount", "0" },
                { "staticCount", "0" }
            }
        };
        await env.Builds.InsertAsync(build);

        Assert.That(build.ExecutionData, Is.Not.Null);

        var executionData = build.ExecutionData!;

        Assert.That(executionData, Contains.Key("trainCount"));
        Assert.That(executionData, Contains.Key("pretranslateCount"));

        int trainCount = Convert.ToInt32(executionData["trainCount"], CultureInfo.InvariantCulture);
        int pretranslateCount = Convert.ToInt32(executionData["pretranslateCount"], CultureInfo.InvariantCulture);
        int staticCount = Convert.ToInt32(executionData["staticCount"], CultureInfo.InvariantCulture);

        Assert.That(trainCount, Is.EqualTo(0));
        Assert.That(pretranslateCount, Is.EqualTo(0));
        Assert.That(staticCount, Is.EqualTo(0));

        await env.PlatformService.UpdateBuildExecutionData(
            new UpdateBuildExecutionDataRequest() { BuildId = "b0", EngineId = engine.Id },
            env.ServerCallContext
        );

        Assert.That(trainCount, Is.GreaterThan(0));
        Assert.That(pretranslateCount, Is.GreaterThan(0));
        Assert.That(staticCount, Is.EqualTo(0));
    }

    [Test]
    public async Task IncrementCorpusSizeAsync()
    {
        var env = new TestEnvironment();
        await env.Engines.InsertAsync(
            new Engine()
            {
                Id = "e0",
                Owner = "owner1",
                Type = "nmt",
                SourceLanguage = "en",
                TargetLanguage = "es",
                Corpora = []
            }
        );
        Assert.That(env.Engines.Get("e0").CorpusSize, Is.EqualTo(0));
        await env.PlatformService.IncrementTranslationEngineCorpusSize(
            new IncrementTranslationEngineCorpusSizeRequest() { EngineId = "e0", Count = 1 },
            env.ServerCallContext
        );
        Assert.That(env.Engines.Get("e0").CorpusSize, Is.EqualTo(1));
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Builds = new MemoryRepository<Build>();
            Engines = new MemoryRepository<Engine>();
            Pretranslations = new MemoryRepository<Pretranslation>();
            DataAccessContext = Substitute.For<IDataAccessContext>();
            PublishEndpoint = Substitute.For<IPublishEndpoint>();
            ServerCallContext = Substitute.For<ServerCallContext>();

            DataAccessContext
                .WithTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
                .Returns(x =>
                {
                    return ((Func<CancellationToken, Task>)x[0])((CancellationToken)x[1]);
                });
            DataAccessContext
                .WithTransactionAsync(Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
                .Returns(x =>
                {
                    return ((Func<CancellationToken, Task>)x[0])((CancellationToken)x[1]);
                });

            PlatformService = new TranslationPlatformServiceV1(
                Builds,
                Engines,
                Pretranslations,
                DataAccessContext,
                PublishEndpoint
            );
        }

        public MemoryRepository<Build> Builds { get; }
        public MemoryRepository<Engine> Engines { get; }
        public MemoryRepository<Pretranslation> Pretranslations { get; }
        public IDataAccessContext DataAccessContext { get; }
        public IPublishEndpoint PublishEndpoint { get; }
        public ServerCallContext ServerCallContext { get; }
        public TranslationPlatformServiceV1 PlatformService { get; }
    }

    private class MockAsyncStreamReader(string engineId) : IAsyncStreamReader<InsertPretranslationsRequest>
    {
        private bool _endOfStream = false;

        public string EngineId { get; } = engineId;
        public InsertPretranslationsRequest Current => new() { EngineId = EngineId };

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            var ret = Task.FromResult(!_endOfStream);
            _endOfStream = true;
            return ret;
        }
    }
}
