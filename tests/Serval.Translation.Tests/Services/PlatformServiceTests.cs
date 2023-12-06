using MassTransit;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

[TestFixture]
public class PlatformServiceTests
{
    [Test]
    public async Task TestBuildStateTransitionsAsync()
    {
        var env = new TestEnvironment();
        await env.Engines.InsertAsync(new Engine() { Id = "e0" });
        await env.Builds.InsertAsync(new Build() { Id = "b0", EngineRef = "e0" });
        await env.PlatformService.BuildStarted(new BuildStartedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That((await env.Builds.GetAsync("b0"))!.State, Is.EqualTo(Shared.Contracts.JobState.Active));
        Assert.That((await env.Engines.GetAsync("e0"))!.IsBuilding);

        await env.PlatformService.BuildCanceled(new BuildCanceledRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That((await env.Builds.GetAsync("b0"))!.State, Is.EqualTo(Shared.Contracts.JobState.Canceled));
        Assert.That(!(await env.Engines.GetAsync("e0"))!.IsBuilding);

        await env.PlatformService.BuildRestarting(
            new BuildRestartingRequest() { BuildId = "b0" },
            env.ServerCallContext
        );
        Assert.That((await env.Builds.GetAsync("b0"))!.State, Is.EqualTo(Shared.Contracts.JobState.Pending));
        Assert.That(!(await env.Engines.GetAsync("e0"))!.IsBuilding);

        Assert.That((await env.Pretranslations.GetAllAsync()).Count, Is.EqualTo(0));
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That((await env.Pretranslations.GetAllAsync()).Count, Is.EqualTo(1));

        await env.PlatformService.BuildFaulted(new BuildFaultedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That((await env.Pretranslations.GetAllAsync()).Count, Is.EqualTo(0));
        Assert.That((await env.Builds.GetAsync("b0"))!.State, Is.EqualTo(Shared.Contracts.JobState.Faulted));
        Assert.That(!(await env.Engines.GetAsync("e0"))!.IsBuilding);

        await env.PlatformService.BuildRestarting(
            new BuildRestartingRequest() { BuildId = "b0" },
            env.ServerCallContext
        );
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That((await env.Pretranslations.GetAllAsync()).Count, Is.EqualTo(1));
        await env.PlatformService.BuildCompleted(new BuildCompletedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That((await env.Pretranslations.GetAllAsync()).Count, Is.EqualTo(1));
        await env.PlatformService.BuildStarted(new BuildStartedRequest() { BuildId = "b0" }, env.ServerCallContext);
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        await env.PlatformService.BuildCompleted(new BuildCompletedRequest() { BuildId = "b0" }, env.ServerCallContext);
        Assert.That((await env.Pretranslations.GetAllAsync()).Count, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateBuildStatusAsync()
    {
        var env = new TestEnvironment();
        await env.Engines.InsertAsync(new Engine() { Id = "e0" });
        await env.Builds.InsertAsync(new Build() { Id = "b0", EngineRef = "e0" });
        Assert.That((await env.Builds.GetAsync("b0"))!.QueueDepth, Is.EqualTo(null));
        Assert.That((await env.Builds.GetAsync("b0"))!.PercentCompleted, Is.EqualTo(null));
        await env.PlatformService.UpdateBuildStatus(
            new UpdateBuildStatusRequest()
            {
                BuildId = "b0",
                QueueDepth = 1,
                PercentCompleted = 0.5
            },
            env.ServerCallContext
        );
        Assert.That((await env.Builds.GetAsync("b0"))!.QueueDepth, Is.EqualTo(1));
        Assert.That((await env.Builds.GetAsync("b0"))!.PercentCompleted, Is.EqualTo(0.5));
    }

    [Test]
    public async Task IncrementCorpusSizeAsync()
    {
        var env = new TestEnvironment();
        await env.Engines.InsertAsync(new Engine() { Id = "e0" });
        Assert.That((await env.Engines.GetAsync("e0"))!.CorpusSize, Is.EqualTo(0));
        await env.PlatformService.IncrementTranslationEngineCorpusSize(
            new IncrementTranslationEngineCorpusSizeRequest() { EngineId = "e0", Count = 1 },
            env.ServerCallContext
        );
        Assert.That((await env.Engines.GetAsync("e0"))!.CorpusSize, Is.EqualTo(1));
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
            PlatformService = new TranslationPlatformServiceV1(
                Builds,
                Engines,
                Pretranslations,
                DataAccessContext,
                PublishEndpoint
            );
        }

        public IRepository<Build> Builds { get; }
        public IRepository<Engine> Engines { get; }
        public IRepository<Pretranslation> Pretranslations { get; }
        public IDataAccessContext DataAccessContext { get; }
        public IPublishEndpoint PublishEndpoint { get; }
        public ServerCallContext ServerCallContext { get; }
        public TranslationPlatformServiceV1 PlatformService { get; }
    }

    private class MockAsyncStreamReader : IAsyncStreamReader<InsertPretranslationRequest>
    {
        private bool _endOfStream;

        public MockAsyncStreamReader(string engineId)
        {
            _endOfStream = false;
            EngineId = engineId;
        }

        public string EngineId { get; }
        public InsertPretranslationRequest Current => new InsertPretranslationRequest() { EngineId = EngineId };

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            var ret = Task.FromResult(!_endOfStream);
            _endOfStream = true;
            return ret;
        }
    }
}
