using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Services;

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
                ParallelCorpora = []
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

        Assert.That(env.WordAlignments.Count, Is.EqualTo(0));
        await env.PlatformService.InsertWordAlignments(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That(env.WordAlignments.Count, Is.EqualTo(1));

        await env.PlatformService.BuildFaulted(new BuildFaultedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.WordAlignments.Count, Is.EqualTo(0));
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.JobState.Faulted));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.BuildRestarting(
            new BuildRestartingRequest() { BuildId = "b0" },
            env.ServerCallContext
        );
        await env.PlatformService.InsertWordAlignments(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That(env.WordAlignments.Count, Is.EqualTo(1));
        await env.PlatformService.BuildCompleted(new BuildCompletedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.WordAlignments.Count, Is.EqualTo(1));
        await env.PlatformService.BuildStarted(new BuildStartedRequest() { BuildId = "b0" }, env.ServerCallContext);
        await env.PlatformService.InsertWordAlignments(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        await env.PlatformService.BuildCompleted(new BuildCompletedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That(env.WordAlignments.Count, Is.EqualTo(1));
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
                ParallelCorpora = []
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
                ParallelCorpora = []
            }
        );
        Assert.That(env.Engines.Get("e0").CorpusSize, Is.EqualTo(0));
        await env.PlatformService.IncrementTrainEngineCorpusSize(
            new IncrementTrainEngineCorpusSizeRequest() { EngineId = "e0", Count = 1 },
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
            WordAlignments = new MemoryRepository<Models.WordAlignment>();
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

            PlatformService = new WordAlignmentPlatformServiceV1(
                Builds,
                Engines,
                WordAlignments,
                DataAccessContext,
                PublishEndpoint
            );
        }

        public MemoryRepository<Build> Builds { get; }
        public MemoryRepository<Engine> Engines { get; }
        public MemoryRepository<Models.WordAlignment> WordAlignments { get; }
        public IDataAccessContext DataAccessContext { get; }
        public IPublishEndpoint PublishEndpoint { get; }
        public ServerCallContext ServerCallContext { get; }
        public WordAlignmentPlatformServiceV1 PlatformService { get; }
    }

    private class MockAsyncStreamReader(string engineId) : IAsyncStreamReader<InsertWordAlignmentsRequest>
    {
        private bool _endOfStream = false;

        public string EngineId { get; } = engineId;
        public InsertWordAlignmentsRequest Current => new() { EngineId = EngineId };

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            var ret = Task.FromResult(!_endOfStream);
            _endOfStream = true;
            return ret;
        }
    }
}
