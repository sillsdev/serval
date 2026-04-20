namespace Serval.Translation.Services;

[TestFixture]
public class BuildServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public async Task GetAllAsync_Success()
    {
        var builds = new MemoryRepository<Build>([
            new Build
            {
                Id = "build1",
                EngineRef = "engine1",
                Owner = "user1",
            },
            new Build
            {
                Id = "build2",
                EngineRef = "engine2",
                Owner = "user2",
            },
        ]);
        var service = new BuildService(builds);

        // SUT
        IEnumerable<Build> actual = await service.GetAllAsync("user1");
        Assert.That(actual.Count, Is.EqualTo(1));
        Assert.That(actual.Single().Id, Is.EqualTo("build1"));
    }

    [Test]
    public async Task GetAllCreatedAfterAsync_Success()
    {
        var builds = new MemoryRepository<Build>([
            new Build
            {
                Id = "build1",
                EngineRef = "engine1",
                Owner = "user1",
                DateCreated = new DateTime(2025, 01, 01),
            },
            new Build
            {
                Id = "build2",
                EngineRef = "engine1",
                Owner = "user1",
                DateCreated = new DateTime(2025, 01, 03),
            },
            new Build
            {
                Id = "build3",
                EngineRef = "engine2",
                Owner = "user2",
                DateCreated = new DateTime(2025, 01, 04),
            },
            new Build
            {
                Id = "build4",
                EngineRef = "engine3",
                Owner = "user1",
                DateCreated = new DateTime(2025, 01, 05),
            },
        ]);
        var service = new BuildService(builds);

        // SUT
        IEnumerable<Build> actual = await service.GetAllCreatedAfterAsync("user1", new DateTime(2025, 01, 02));
        Assert.That(actual.Count, Is.EqualTo(2));
        // Build 1 is missed because it is too old
        Assert.That(actual.First().Id, Is.EqualTo("build2"));
        // Build 3 is missed because it is for another user
        Assert.That(actual.Last().Id, Is.EqualTo("build4"));
    }

    [Test]
    public async Task GetNextFinishedBuildAsync_Insert()
    {
        var builds = new MemoryRepository<Build>();
        var service = new BuildService(builds);
        Task<EntityChange<Build>> task = service.GetNextFinishedBuildAsync("user1", DateTime.UtcNow.AddMinutes(-1));
        var build = new Build
        {
            Id = BUILD1_ID,
            EngineRef = "engine1",
            Owner = "user1",
            State = JobState.Completed,
            DateFinished = DateTime.UtcNow,
        };
        await builds.InsertAsync(build);
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Insert));
        Assert.That(change.Entity?.State, Is.EqualTo(JobState.Completed));
    }

    [Test]
    public async Task GetNextFinishedBuildAsync_Update()
    {
        var builds = new MemoryRepository<Build>();
        var service = new BuildService(builds);
        var build = new Build
        {
            Id = BUILD1_ID,
            EngineRef = "engine1",
            Owner = "user1",
        };
        await builds.InsertAsync(build);
        Task<EntityChange<Build>> task = service.GetNextFinishedBuildAsync("user1", DateTime.UtcNow.AddMinutes(-1));
        await builds.UpdateAsync(
            build,
            u =>
            {
                u.Set(b => b.State, JobState.Completed);
                u.Set(b => b.DateFinished, DateTime.UtcNow);
            }
        );
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Update));
        Assert.That(change.Entity?.State, Is.EqualTo(JobState.Completed));
    }
}
