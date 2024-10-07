using Serval.Engine.V1;
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
            new TranslationEngine()
            {
                Id = "e0",
                Owner = "owner1",
                Type = "nmt",
                SourceLanguage = "en",
                TargetLanguage = "es",
                Corpora = []
            }
        );
        await env.Builds.InsertAsync(new TranslationBuild() { Id = "b0", EngineRef = "e0" });
        await env.PlatformService.JobStarted(new JobStartedRequest() { JobId = "b0" }, env.ServerCallContext);
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.BuildState.Active));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.True);

        await env.PlatformService.JobCanceled(new JobCanceledRequest() { JobId = "b0" }, env.ServerCallContext);
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.BuildState.Canceled));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.JobRestarting(new JobRestartingRequest() { JobId = "b0" }, env.ServerCallContext);
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.BuildState.Pending));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        Assert.That(env.Pretranslations.Count, Is.EqualTo(0));
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));

        await env.PlatformService.JobFaulted(new JobFaultedRequest() { JobId = "b0" }, env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(0));
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(Shared.Contracts.BuildState.Faulted));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.JobRestarting(new JobRestartingRequest() { JobId = "b0" }, env.ServerCallContext);
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
        await env.PlatformService.JobCompleted(new JobCompletedRequest() { JobId = "b0" }, env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
        await env.PlatformService.JobStarted(new JobStartedRequest() { JobId = "b0" }, env.ServerCallContext);
        await env.PlatformService.InsertPretranslations(new MockAsyncStreamReader("e0"), env.ServerCallContext);
        await env.PlatformService.JobCompleted(new JobCompletedRequest() { JobId = "b0" }, env.ServerCallContext);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateJobStatusAsync()
    {
        var env = new TestEnvironment();
        await env.Engines.InsertAsync(
            new TranslationEngine()
            {
                Id = "e0",
                Owner = "owner1",
                Type = "nmt",
                SourceLanguage = "en",
                TargetLanguage = "es",
                Corpora = []
            }
        );
        await env.Builds.InsertAsync(new TranslationBuild() { Id = "b0", EngineRef = "e0" });
        Assert.That(env.Builds.Get("b0").QueueDepth, Is.Null);
        Assert.That(env.Builds.Get("b0").PercentCompleted, Is.Null);
        await env.PlatformService.UpdateJobStatus(
            new UpdateJobStatusRequest()
            {
                JobId = "b0",
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
            new TranslationEngine()
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
            Builds = new MemoryRepository<TranslationBuild>();
            Engines = new MemoryRepository<TranslationEngine>();
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

        public MemoryRepository<TranslationBuild> Builds { get; }
        public MemoryRepository<TranslationEngine> Engines { get; }
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
