namespace Serval.Translation.Services;

[TestFixture]
public class BuildCleanupServiceTests
{
    [Test]
    public async Task CleanupAsync()
    {
        TestEnvironment env = new();
        Assert.That(env.Builds.Count, Is.EqualTo(2));
        await env.CheckBuildsAsync();
        Assert.That(env.Builds.Count, Is.EqualTo(1));
        Assert.That((await env.Builds.GetAllAsync())[0].Id, Is.EqualTo("build2"));
    }

    private class TestEnvironment
    {
        public MemoryRepository<Build> Builds { get; }

        public TestEnvironment()
        {
            Builds = new MemoryRepository<Build>();
            Builds.Add(
                new Build
                {
                    Id = "build1",
                    EngineRef = "engine1",
                    IsInitialized = false,
                    DateCreated = DateTime.UtcNow.Subtract(TimeSpan.FromHours(10))
                }
            );
            Builds.Add(
                new Build
                {
                    Id = "build2",
                    EngineRef = "engine2",
                    IsInitialized = true,
                    DateCreated = DateTime.UtcNow.Subtract(TimeSpan.FromHours(10))
                }
            );

            Service = new BuildCleanupService(
                Substitute.For<IServiceProvider>(),
                Substitute.For<ILogger<BuildCleanupService>>(),
                TimeSpan.Zero
            );
        }

        public BuildCleanupService Service { get; }

        public async Task CheckBuildsAsync()
        {
            await Service.CheckEntitiesAsync(Builds, CancellationToken.None);
        }
    }
}
