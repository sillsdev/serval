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
                ParallelCorpora = [],
            }
        );
        await env.Builds.InsertAsync(new Build() { Id = "b0", EngineRef = "e0" });
        await env.PlatformService.BuildStartedAsync("b0");
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Active));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.True);

        await env.PlatformService.BuildCanceledAsync("b0");
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Canceled));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.BuildRestartingAsync("b0");
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Pending));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        Assert.That(env.WordAlignments.Count, Is.EqualTo(0));
        await env.PlatformService.InsertWordAlignmentsAsync("e0", GetTestWordAlignments());
        Assert.That(env.WordAlignments.Count, Is.EqualTo(1));

        await env.PlatformService.BuildFaultedAsync("b0", "Faulted");
        Assert.That(env.WordAlignments.Count, Is.EqualTo(0));
        Assert.That(env.Builds.Get("b0").State, Is.EqualTo(JobState.Faulted));
        Assert.That(env.Engines.Get("e0").IsBuilding, Is.False);

        await env.PlatformService.BuildRestartingAsync("b0");
        await env.PlatformService.InsertWordAlignmentsAsync("e0", GetTestWordAlignments());
        Assert.That(env.WordAlignments.Count, Is.EqualTo(1));
        await env.PlatformService.BuildCompletedAsync("b0", 0, 0.0);
        Assert.That(env.WordAlignments.Count, Is.EqualTo(1));
        await env.PlatformService.BuildStartedAsync("b0");
        await env.PlatformService.InsertWordAlignmentsAsync("e0", GetTestWordAlignments());
        await env.PlatformService.BuildCompletedAsync("b0", 0, 0.0);
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
                ParallelCorpora = [],
            }
        );
        await env.Builds.InsertAsync(new Build() { Id = "b0", EngineRef = "e0" });
        Assert.That(env.Builds.Get("b0").QueueDepth, Is.Null);
        Assert.That(env.Builds.Get("b0").Progress, Is.Null);

        await env.PlatformService.BuildStartedAsync("b0");
        await env.PlatformService.UpdateBuildStatusAsync(
            "b0",
            new() { PercentCompleted = 0.5 },
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
            ParallelCorpora = [],
        };
        await env.Engines.InsertAsync(engine);

        var build = new Build()
        {
            Id = "123",
            EngineRef = "e0",
            ExecutionData = new() { TrainCount = 0, WordAlignCount = 0 },
        };
        await env.Builds.InsertAsync(build);

        Assert.That(build.ExecutionData, Is.Not.Null);

        ExecutionData? executionData = build.ExecutionData;

        Assert.That(executionData.TrainCount, Is.EqualTo(0));
        Assert.That(executionData.WordAlignCount, Is.EqualTo(0));

        await env.PlatformService.UpdateBuildExecutionDataAsync(
            engine.Id,
            build.Id,
            new() { TrainCount = 4, WordAlignCount = 5 }
        );

        build = await env.Builds.GetAsync(c => c.Id == build.Id);

        executionData = build?.ExecutionData;

        Assert.That(executionData, Is.Not.Null);
        Assert.That(executionData.TrainCount, Is.GreaterThan(0));
        Assert.That(executionData.WordAlignCount, Is.GreaterThan(0));
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
                ParallelCorpora = [],
            }
        );
        Assert.That(env.Engines.Get("e0").CorpusSize, Is.EqualTo(0));
        await env.PlatformService.IncrementEngineCorpusSizeAsync("e0", 1);
        Assert.That(env.Engines.Get("e0").CorpusSize, Is.EqualTo(1));
    }

    private static async IAsyncEnumerable<WordAlignmentContract> GetTestWordAlignments()
    {
        yield return new()
        {
            CorpusId = "corpus1",
            TextId = "text1",
            SourceRefs = ["ref1"],
            TargetRefs = ["ref1"],
            SourceTokens = ["esto"],
            TargetTokens = ["this"],
            Alignment = [new() { SourceIndex = 0, TargetIndex = 0 }],
        };
        await Task.CompletedTask;
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Builds = new MemoryRepository<Build>();
            Engines = new MemoryRepository<Engine>();
            WordAlignments = new MemoryRepository<Models.WordAlignment>();
            DataAccessContext = Substitute.For<IDataAccessContext>();
            EventRouter = Substitute.For<IEventRouter>();

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

            PlatformService = new PlatformService(Builds, Engines, WordAlignments, DataAccessContext, EventRouter);
        }

        public MemoryRepository<Build> Builds { get; }
        public MemoryRepository<Engine> Engines { get; }
        public MemoryRepository<Models.WordAlignment> WordAlignments { get; }
        public IDataAccessContext DataAccessContext { get; }
        public IEventRouter EventRouter { get; }
        public PlatformService PlatformService { get; }
    }
}
