namespace Serval.Translation.Services;

[TestFixture]
public class BuildServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public async Task GetActiveNewerRevisionAsync_Insert()
    {
        var builds = new MemoryRepository<TranslationBuildJob>();
        var service = new JobService<TranslationBuildJob>(builds);
        Task<EntityChange<TranslationBuildJob>> task = service.GetActiveNewerRevisionAsync("engine1", 1);
        var build = new TranslationBuildJob
        {
            Id = BUILD1_ID,
            EngineRef = "engine1",
            PercentCompleted = 0.1
        };
        await builds.InsertAsync(build);
        EntityChange<TranslationBuildJob> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Insert));
        Assert.That(change.Entity!.Revision, Is.EqualTo(1));
        Assert.That(change.Entity.PercentCompleted, Is.EqualTo(0.1));
    }

    [Test]
    public async Task GetNewerRevisionAsync_Update()
    {
        var builds = new MemoryRepository<TranslationBuildJob>();
        var service = new JobService<TranslationBuildJob>(builds);
        var build = new TranslationBuildJob { Id = BUILD1_ID, EngineRef = "engine1" };
        await builds.InsertAsync(build);
        Task<EntityChange<TranslationBuildJob>> task = service.GetNewerRevisionAsync(build.Id, 2);
        await builds.UpdateAsync(build, u => u.Set(b => b.PercentCompleted, 0.1));
        EntityChange<TranslationBuildJob> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Update));
        Assert.That(change.Entity!.Revision, Is.EqualTo(2));
        Assert.That(change.Entity.PercentCompleted, Is.EqualTo(0.1));
    }

    [Test]
    public async Task GetNewerRevisionAsync_Delete()
    {
        var builds = new MemoryRepository<TranslationBuildJob>();
        var service = new JobService<TranslationBuildJob>(builds);
        var build = new TranslationBuildJob { Id = BUILD1_ID, EngineRef = "engine1" };
        await builds.InsertAsync(build);
        Task<EntityChange<TranslationBuildJob>> task = service.GetNewerRevisionAsync(build.Id, 2);
        await builds.DeleteAsync(build);
        EntityChange<TranslationBuildJob> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }

    [Test]
    public async Task GetNewerRevisionAsync_DoesNotExist()
    {
        var service = new JobService<TranslationBuildJob>(new MemoryRepository<TranslationBuildJob>());
        EntityChange<TranslationBuildJob> change = await service.GetNewerRevisionAsync("build1", 2);
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }
}
