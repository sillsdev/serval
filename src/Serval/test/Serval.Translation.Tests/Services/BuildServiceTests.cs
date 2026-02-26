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
    public async Task GetNextCompletedBuildAsync_Insert()
    {
        var builds = new MemoryRepository<Build>();
        var service = new BuildService(builds);
        Task<EntityChange<Build>> task = service.GetNextCompletedBuildAsync("user1", DateTime.UtcNow.AddMinutes(-1));
        var build = new Build
        {
            Id = BUILD1_ID,
            EngineRef = "engine1",
            Owner = "user1",
            State = Shared.Contracts.JobState.Completed,
            DateFinished = DateTime.UtcNow,
        };
        await builds.InsertAsync(build);
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Insert));
        Assert.That(change.Entity?.State, Is.EqualTo(Shared.Contracts.JobState.Completed));
    }

    [Test]
    public async Task GetNextCompletedBuildAsync_Update()
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
        Task<EntityChange<Build>> task = service.GetNextCompletedBuildAsync("user1", DateTime.UtcNow.AddMinutes(-1));
        await builds.UpdateAsync(
            build,
            u =>
            {
                u.Set(b => b.State, Shared.Contracts.JobState.Completed);
                u.Set(b => b.DateFinished, DateTime.UtcNow);
            }
        );
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Update));
        Assert.That(change.Entity?.State, Is.EqualTo(Shared.Contracts.JobState.Completed));
    }

    [Test]
    public async Task GetActiveNewerRevisionAsync_Insert()
    {
        var builds = new MemoryRepository<Build>();
        var service = new BuildService(builds);
        Task<EntityChange<Build>> task = service.GetActiveNewerRevisionAsync("engine1", 1);
        var build = new Build
        {
            Id = BUILD1_ID,
            EngineRef = "engine1",
            Owner = "user1",
            Progress = 0.1,
        };
        await builds.InsertAsync(build);
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Insert));
        Assert.That(change.Entity!.Revision, Is.EqualTo(1));
        Assert.That(change.Entity.Progress, Is.EqualTo(0.1));
    }

    [Test]
    public async Task GetNewerRevisionAsync_Update()
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
        Task<EntityChange<Build>> task = service.GetNewerRevisionAsync(build.Id, 2);
        await builds.UpdateAsync(build, u => u.Set(b => b.Progress, 0.1));
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Update));
        Assert.That(change.Entity!.Revision, Is.EqualTo(2));
        Assert.That(change.Entity.Progress, Is.EqualTo(0.1));
    }

    [Test]
    public async Task GetNewerRevisionAsync_Delete()
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
        Task<EntityChange<Build>> task = service.GetNewerRevisionAsync(build.Id, 2);
        await builds.DeleteAsync(build);
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }

    [Test]
    public async Task GetNewerRevisionAsync_DoesNotExist()
    {
        var builds = new MemoryRepository<Build>();
        var service = new BuildService(builds);
        EntityChange<Build> change = await service.GetNewerRevisionAsync("build1", 2);
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }
}
