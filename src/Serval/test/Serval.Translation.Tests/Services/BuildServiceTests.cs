namespace Serval.Translation.Services;

[TestFixture]
public class BuildServiceTests
{
    const string BUILD1_ID = "b00000000000000000000001";

    [Test]
    public async Task GetActiveNewerRevisionAsync_Insert()
    {
        var builds = new MemoryRepository<TranslationBuild>();
        var service = new BuildService<TranslationBuild>(builds);
        Task<EntityChange<TranslationBuild>> task = service.GetActiveNewerRevisionAsync("engine1", 1);
        var build = new TranslationBuild
        {
            Id = BUILD1_ID,
            EngineRef = "engine1",
            PercentCompleted = 0.1
        };
        await builds.InsertAsync(build);
        EntityChange<TranslationBuild> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Insert));
        Assert.That(change.Entity!.Revision, Is.EqualTo(1));
        Assert.That(change.Entity.PercentCompleted, Is.EqualTo(0.1));
    }

    [Test]
    public async Task GetNewerRevisionAsync_Update()
    {
        var builds = new MemoryRepository<TranslationBuild>();
        var service = new BuildService<TranslationBuild>(builds);
        var build = new TranslationBuild { Id = BUILD1_ID, EngineRef = "engine1" };
        await builds.InsertAsync(build);
        Task<EntityChange<TranslationBuild>> task = service.GetNewerRevisionAsync(build.Id, 2);
        await builds.UpdateAsync(build, u => u.Set(b => b.PercentCompleted, 0.1));
        EntityChange<TranslationBuild> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Update));
        Assert.That(change.Entity!.Revision, Is.EqualTo(2));
        Assert.That(change.Entity.PercentCompleted, Is.EqualTo(0.1));
    }

    [Test]
    public async Task GetNewerRevisionAsync_Delete()
    {
        var builds = new MemoryRepository<TranslationBuild>();
        var service = new BuildService<TranslationBuild>(builds);
        var build = new TranslationBuild { Id = BUILD1_ID, EngineRef = "engine1" };
        await builds.InsertAsync(build);
        Task<EntityChange<TranslationBuild>> task = service.GetNewerRevisionAsync(build.Id, 2);
        await builds.DeleteAsync(build);
        EntityChange<TranslationBuild> change = await task;
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }

    [Test]
    public async Task GetNewerRevisionAsync_DoesNotExist()
    {
        var service = new BuildService<TranslationBuild>(new MemoryRepository<TranslationBuild>());
        EntityChange<TranslationBuild> change = await service.GetNewerRevisionAsync("build1", 2);
        Assert.That(change.Type, Is.EqualTo(EntityChangeType.Delete));
    }
}
