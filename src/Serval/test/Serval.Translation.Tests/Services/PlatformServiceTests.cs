using Serval.Translation.V1;
using ExecutionData = Serval.Translation.Models.ExecutionData;
using ParallelCorpus = Serval.Shared.Models.ParallelCorpus;
using PhaseStage = Serval.Translation.V1.PhaseStage;

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
        Assert.That(env.Builds.Get("b0").Progress, Is.Null);
        var request = new UpdateBuildStatusRequest
        {
            BuildId = "b0",
            QueueDepth = 1,
            Progress = 0.5
        };
        request.Phases.Add(
            new Phase
            {
                Stage = PhaseStage.Train,
                Step = 2,
                StepCount = 3
            }
        );
        await env.PlatformService.UpdateBuildStatus(request, env.ServerCallContext);
        Assert.That(env.Builds.Get("b0").QueueDepth, Is.EqualTo(1));
        Assert.That(env.Builds.Get("b0").Progress, Is.EqualTo(0.5));
        Assert.That(env.Builds.Get("b0").Phases![0].Stage, Is.EqualTo(BuildPhaseStage.Train));
        Assert.That(env.Builds.Get("b0").Phases![0].Step, Is.EqualTo(2));
        Assert.That(env.Builds.Get("b0").Phases![0].StepCount, Is.EqualTo(3));
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

        var build = new Build()
        {
            Id = "123",
            EngineRef = "e0",
            ExecutionData = new ExecutionData { TrainCount = 0, PretranslateCount = 0 }
        };
        await env.Builds.InsertAsync(build);

        Assert.That(build.ExecutionData, Is.Not.Null);

        var executionData = build.ExecutionData;

        Assert.That(executionData.TrainCount, Is.EqualTo(0));
        Assert.That(executionData.PretranslateCount, Is.EqualTo(0));

        var updateRequest = new UpdateBuildExecutionDataRequest()
        {
            BuildId = "123",
            EngineId = engine.Id,
            ExecutionData = new V1.ExecutionData { TrainCount = 4, PretranslateCount = 5, }
        };

        await env.PlatformService.UpdateBuildExecutionData(updateRequest, env.ServerCallContext);

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        executionData = build?.ExecutionData;

        Assert.That(executionData, Is.Not.Null);
        Assert.That(executionData.TrainCount, Is.GreaterThan(0));
        Assert.That(executionData.PretranslateCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task UpdateParallelCorpusAnalysisAsync()
    {
        var env = new TestEnvironment();

        var engine = new Engine
        {
            Id = "e0",
            Owner = "owner1",
            Type = "nmt",
            SourceLanguage = "en",
            TargetLanguage = "es",
            ParallelCorpora =
            [
                new ParallelCorpus
                {
                    Id = "parallelCorpus01",
                    SourceCorpora = [],
                    TargetCorpora = [],
                },
            ],
        };
        await env.Engines.InsertAsync(engine);

        var build = new Build { Id = "123", EngineRef = "e0" };
        await env.Builds.InsertAsync(build);

        List<ParallelCorpusAnalysis> expected =
        [
            new ParallelCorpusAnalysis
            {
                ParallelCorpusRef = "parallelCorpus01",
                TargetQuoteConvention = "typewriter_english",
            },
        ];

        var updateRequest = new UpdateParallelCorpusAnalysisRequest { BuildId = "123", EngineId = engine.Id };
        updateRequest.ParallelCorpusAnalysis.Add(
            new ParallelCorpusAnalysisResult
            {
                ParallelCorpusId = "parallelCorpus01",
                TargetQuoteConvention = "typewriter_english",
            }
        );

        await env.PlatformService.UpdateParallelCorpusAnalysis(updateRequest, env.ServerCallContext);

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        Assert.That(build?.Analysis, Is.EqualTo(expected));
    }

    [Test]
    public async Task UpdateParallelCorpusAnalysisAsync_NoEngine()
    {
        var env = new TestEnvironment();

        var build = new Build { Id = "123", EngineRef = "e0" };
        await env.Builds.InsertAsync(build);

        var updateRequest = new UpdateParallelCorpusAnalysisRequest { BuildId = "123", EngineId = "e0" };
        await env.PlatformService.UpdateParallelCorpusAnalysis(updateRequest, env.ServerCallContext);

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        Assert.That(build?.Analysis, Is.Null);
    }

    [Test]
    public async Task UpdateParallelCorpusAnalysisAsync_NoParallelCorpora()
    {
        var env = new TestEnvironment();

        var engine = new Engine
        {
            Id = "e0",
            Owner = "owner1",
            Type = "nmt",
            SourceLanguage = "en",
            TargetLanguage = "es",
            ParallelCorpora = [],
        };
        await env.Engines.InsertAsync(engine);

        var build = new Build { Id = "123", EngineRef = "e0" };
        await env.Builds.InsertAsync(build);

        var updateRequest = new UpdateParallelCorpusAnalysisRequest { BuildId = "123", EngineId = engine.Id };
        await env.PlatformService.UpdateParallelCorpusAnalysis(updateRequest, env.ServerCallContext);

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        Assert.That(build?.Analysis, Is.Null);
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
        await env.PlatformService.IncrementEngineCorpusSize(
            new IncrementEngineCorpusSizeRequest() { EngineId = "e0", Count = 1 },
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
