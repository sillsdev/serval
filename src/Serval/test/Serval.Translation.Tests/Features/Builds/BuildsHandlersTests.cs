namespace Serval.Translation.Features.Builds;

[TestFixture]
public class BuildsHandlersTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public async Task GetAllBuildsCreatedAfter_NoFilter_Success()
    {
        TestEnvironment env = new();
        await env.Builds.InsertAsync(
            new()
            {
                Id = "build1",
                EngineRef = "engine1",
                Owner = "user1",
            }
        );
        await env.Builds.InsertAsync(
            new()
            {
                Id = "build2",
                EngineRef = "engine2",
                Owner = "user2",
            }
        );

        GetAllBuildsCreatedAfterHandler handler = new(env.Builds, env.DtoMapper);

        GetAllBuildsCreatedAfterResponse response = await handler.HandleAsync(new("user1", null));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Builds.Count(), Is.EqualTo(1));
            Assert.That(response.Builds.Single().Id, Is.EqualTo("build1"));
        }
    }

    [Test]
    public async Task GetAllBuildsCreatedAfter_WithFilter_Success()
    {
        TestEnvironment env = new();
        await env.Builds.InsertAsync(
            new()
            {
                Id = "build1",
                EngineRef = "engine1",
                Owner = "user1",
                DateCreated = new DateTime(2025, 01, 01),
            }
        );
        await env.Builds.InsertAsync(
            new()
            {
                Id = "build2",
                EngineRef = "engine1",
                Owner = "user1",
                DateCreated = new DateTime(2025, 01, 03),
            }
        );
        await env.Builds.InsertAsync(
            new()
            {
                Id = "build3",
                EngineRef = "engine2",
                Owner = "user2",
                DateCreated = new DateTime(2025, 01, 04),
            }
        );
        await env.Builds.InsertAsync(
            new()
            {
                Id = "build4",
                EngineRef = "engine3",
                Owner = "user1",
                DateCreated = new DateTime(2025, 01, 05),
            }
        );

        GetAllBuildsCreatedAfterHandler handler = new(env.Builds, env.DtoMapper);

        GetAllBuildsCreatedAfterResponse response = await handler.HandleAsync(new("user1", new DateTime(2025, 01, 02)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Builds.Count(), Is.EqualTo(2));
            // Build 1 is missed because it is too old
            Assert.That(response.Builds.First().Id, Is.EqualTo("build2"));
            // Build 3 is missed because it is for another user
            Assert.That(response.Builds.Last().Id, Is.EqualTo("build4"));
        }
    }

    [Test]
    public async Task GetNextFinishedBuild_Insert()
    {
        TestEnvironment env = new();
        GetNextFinishedBuildHandler handler = new(env.Builds, env.DtoMapper, env.ApiOptions);
        Task<GetNextFinishedBuildResponse> task = handler.HandleAsync(new("user1", DateTime.UtcNow.AddMinutes(-1)));

        await env.Builds.InsertAsync(
            new()
            {
                Id = BUILD1_ID,
                EngineRef = "engine1",
                Owner = "user1",
                State = JobState.Completed,
                DateFinished = DateTime.UtcNow,
            }
        );
        GetNextFinishedBuildResponse response = await task;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.TimedOut, Is.False);
            Assert.That(response.Build?.State, Is.EqualTo(JobState.Completed));
        }
    }

    [Test]
    public async Task GetNextFinishedBuild_Update()
    {
        TestEnvironment env = new();
        Build build = new()
        {
            Id = BUILD1_ID,
            EngineRef = "engine1",
            Owner = "user1",
        };
        await env.Builds.InsertAsync(build);

        GetNextFinishedBuildHandler handler = new(env.Builds, env.DtoMapper, env.ApiOptions);
        Task<GetNextFinishedBuildResponse> task = handler.HandleAsync(new("user1", DateTime.UtcNow.AddMinutes(-1)));

        await env.Builds.UpdateAsync(
            build,
            u =>
            {
                u.Set(b => b.State, JobState.Completed);
                u.Set(b => b.DateFinished, DateTime.UtcNow);
            }
        );
        GetNextFinishedBuildResponse response = await task;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.TimedOut, Is.False);
            Assert.That(response.Build?.State, Is.EqualTo(JobState.Completed));
        }
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Builds = new MemoryRepository<Build>();
            ApiOptions = Substitute.For<IOptionsMonitor<ApiOptions>>();
            ApiOptions.CurrentValue.Returns(new ApiOptions());
            DtoMapper = new DtoMapper(Substitute.For<IUrlService>());
        }

        public MemoryRepository<Build> Builds { get; }
        public IOptionsMonitor<ApiOptions> ApiOptions { get; }
        public DtoMapper DtoMapper { get; }
    }
}
