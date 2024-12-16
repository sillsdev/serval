namespace Serval.WordAlignment.Services;

[TestFixture]
public class EngineCleanupServiceTests
{
    [Test]
    public async Task CleanupAsync()
    {
        TestEnvironment env = new();
        Assert.That(env.Engines.Count, Is.EqualTo(2));
        await env.CheckEnginesAsync();
        Assert.That(env.Engines.Count, Is.EqualTo(1));
        Assert.That((await env.Engines.GetAllAsync())[0].Id, Is.EqualTo("engine2"));
    }

    private class TestEnvironment
    {
        public MemoryRepository<Engine> Engines { get; }

        public TestEnvironment()
        {
            Engines = new MemoryRepository<Engine>();
            Engines.Add(
                new Engine
                {
                    Id = "engine1",
                    SourceLanguage = "en",
                    TargetLanguage = "es",
                    Type = "Nmt",
                    Owner = "client1",
                    IsInitialized = false,
                    DateCreated = DateTime.UtcNow.Subtract(TimeSpan.FromHours(10)),
                    ParallelCorpora = []
                }
            );
            Engines.Add(
                new Engine
                {
                    Id = "engine2",
                    SourceLanguage = "en",
                    TargetLanguage = "es",
                    Type = "Nmt",
                    Owner = "client1",
                    IsInitialized = true,
                    DateCreated = DateTime.UtcNow.Subtract(TimeSpan.FromHours(10)),
                    ParallelCorpora = []
                }
            );

            Service = new EngineCleanupService(
                Substitute.For<IServiceProvider>(),
                Substitute.For<ILogger<EngineCleanupService>>(),
                TimeSpan.Zero
            );
        }

        public EngineCleanupService Service { get; }

        public async Task CheckEnginesAsync()
        {
            await Service.CheckEntitiesAsync(Engines, CancellationToken.None);
        }
    }
}
