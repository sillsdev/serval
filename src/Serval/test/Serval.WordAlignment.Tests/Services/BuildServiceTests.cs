namespace Serval.WordAlignment.Services;

[TestFixture]
public class BuildServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

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
        var build = new Build { Id = BUILD1_ID, EngineRef = "engine1" };
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
        var build = new Build { Id = BUILD1_ID, EngineRef = "engine1" };
        await builds.InsertAsync(build);
        Task<EntityChange<Build>> task = service.GetNewerRevisionAsync(build.Id, 2);
        await builds.DeleteAsync(build);
        EntityChange<Build> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }

    [Test]
    public async Task GetNewerRevisionAsync_DoesNotExist()
    {
        var service = new BuildService(new MemoryRepository<Build>());
        EntityChange<Build> change = await service.GetNewerRevisionAsync("build1", 2);
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }
}
