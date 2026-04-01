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
                Corpora = [],
            }
        );
        await env.Builds.InsertAsync(
            new Build()
            {
                Id = "b0",
                EngineRef = "e0",
                Owner = "owner1",
            }
        );
        await env.PlatformService.BuildStartedAsync("b0");
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Active));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.True);

        await env.PlatformService.BuildCanceledAsync("b0");
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Canceled));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.BuildRestartingAsync("b0");
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Pending));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        Assert.That(env.Pretranslations.Count, Is.EqualTo(0));
        await env.PlatformService.InsertPretranslationsAsync("e0", GetTestPretranslations());
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));

        await env.PlatformService.BuildFaultedAsync("b0", "");
        Assert.That(env.Pretranslations.Count, Is.EqualTo(0));
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Faulted));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.BuildRestartingAsync("b0");
        await env.PlatformService.InsertPretranslationsAsync("e0", GetTestPretranslations());
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
        await env.PlatformService.BuildCompletedAsync("b0", 0, 0.0);
        Assert.That(env.Pretranslations.Count, Is.EqualTo(1));
        await env.PlatformService.BuildStartedAsync("b0");
        await env.PlatformService.InsertPretranslationsAsync("e0", GetTestPretranslations());
        await env.PlatformService.BuildCompletedAsync("b0", 0, 0.0);
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
                Corpora = [],
            }
        );
        await env.Builds.InsertAsync(
            new Build()
            {
                Id = "b0",
                EngineRef = "e0",
                Owner = "owner1",
            }
        );
        Assert.That(env.Builds.Get("b0").QueueDepth, Is.Null);
        Assert.That(env.Builds.Get("b0").Progress, Is.Null);
        await env.PlatformService.UpdateBuildStatusAsync(
            "b0",
            new() { Step = 0, PercentCompleted = 0.5 },
            queueDepth: 1,
            phases:
            [
                new()
                {
                    Stage = PhaseStage.Train,
                    Step = 2,
                    StepCount = 3,
                },
            ]
        );
        Assert.That(env.Builds.Get("b0").QueueDepth, Is.EqualTo(1));
        Assert.That(env.Builds.Get("b0").Progress, Is.EqualTo(0.5));
        Assert.That(env.Builds.Get("b0").Phases![0].Stage, Is.EqualTo(PhaseStage.Train));
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
            Corpora = [],
        };
        await env.Engines.InsertAsync(engine);

        var build = new Build()
        {
            Id = "123",
            EngineRef = "e0",
            Owner = "owner1",
            ExecutionData = new() { TrainCount = 0, PretranslateCount = 0 },
        };
        await env.Builds.InsertAsync(build);

        Assert.That(build.ExecutionData, Is.Not.Null);

        var executionData = build.ExecutionData;

        Assert.That(executionData.TrainCount, Is.EqualTo(0));
        Assert.That(executionData.PretranslateCount, Is.EqualTo(0));

        await env.PlatformService.UpdateBuildExecutionDataAsync(
            engine.Id,
            "123",
            new() { TrainCount = 4, PretranslateCount = 5 }
        );

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        executionData = build?.ExecutionData;

        Assert.That(executionData, Is.Not.Null);
        Assert.That(executionData.TrainCount, Is.GreaterThan(0));
        Assert.That(executionData.PretranslateCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task UpdateTargetQuoteConventionAsync()
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
                new()
                {
                    Id = "parallelCorpus01",
                    SourceCorpora = [],
                    TargetCorpora = [],
                },
            ],
        };
        await env.Engines.InsertAsync(engine);

        var build = new Build
        {
            Id = "123",
            EngineRef = "e0",
            Owner = "owner1",
        };
        await env.Builds.InsertAsync(build);

        string expected = "typewriter_english";

        await env.PlatformService.UpdateTargetQuoteConventionAsync(engine.Id, "123", "typewriter_english");

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        Assert.Multiple(() =>
        {
            Assert.That(build?.TargetQuoteConvention, Is.EqualTo(expected));
            Assert.That(build?.Analysis, Has.Count.EqualTo(1));
        });
        Assert.That(build?.Analysis?[0].TargetQuoteConvention, Is.EqualTo(expected));
    }

    [Test]
    public async Task UpdateTargetQuoteConventionAsync_NoEngine()
    {
        var env = new TestEnvironment();

        var build = new Build
        {
            Id = "123",
            EngineRef = "e0",
            Owner = "owner1",
        };
        await env.Builds.InsertAsync(build);

        await env.PlatformService.UpdateTargetQuoteConventionAsync("e0", "123", "");

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        Assert.Multiple(() =>
        {
            Assert.That(build?.TargetQuoteConvention, Is.Null);
            Assert.That(build?.Analysis, Is.Null);
        });
    }

    [Test]
    public async Task UpdateTargetQuoteConventionAsync_NoParallelCorpora()
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

        var build = new Build
        {
            Id = "123",
            EngineRef = "e0",
            Owner = "owner1",
        };
        await env.Builds.InsertAsync(build);

        await env.PlatformService.UpdateTargetQuoteConventionAsync(engine.Id, "123", "");

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        Assert.Multiple(() =>
        {
            Assert.That(build?.TargetQuoteConvention, Is.EqualTo(""));
            Assert.That(build?.Analysis, Has.Count.EqualTo(0));
        });
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
                Corpora = [],
            }
        );
        Assert.That(env.Engines.Get("e0").CorpusSize, Is.EqualTo(0));
        await env.PlatformService.IncrementEngineCorpusSizeAsync("e0", 1);
        Assert.That(env.Engines.Get("e0").CorpusSize, Is.EqualTo(1));
    }

    private static async IAsyncEnumerable<PretranslationContract> GetTestPretranslations()
    {
        yield return new PretranslationContract
        {
            CorpusId = "e0",
            TextId = "text1",
            SourceRefs = ["ref1"],
            TargetRefs = ["ref1"],
            Translation = "test",
        };
        await Task.CompletedTask;
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Builds = new MemoryRepository<Build>();
            Engines = new MemoryRepository<Engine>();
            Pretranslations = new MemoryRepository<Pretranslation>();
            DataAccessContext = Substitute.For<IDataAccessContext>();

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

            PlatformService = new PlatformService(
                Builds,
                Engines,
                Pretranslations,
                DataAccessContext,
                Substitute.For<IEventRouter>()
            );
        }

        public MemoryRepository<Build> Builds { get; }
        public MemoryRepository<Engine> Engines { get; }
        public MemoryRepository<Pretranslation> Pretranslations { get; }
        public IDataAccessContext DataAccessContext { get; }
        public PlatformService PlatformService { get; }
    }
}
