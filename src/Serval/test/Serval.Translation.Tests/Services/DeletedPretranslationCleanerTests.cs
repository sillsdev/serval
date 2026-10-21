namespace Serval.Translation.Services;

[TestFixture]
public class DeletedPretranslationCleanerTests
{
    [Test]
    public async Task Clean()
    {
        var env = new TestEnvironment();
        env.Pretranslations.Add(
            new Pretranslation
            {
                Id = "pretranslation1",
                Revision = 0,
                EngineRef = "engine1",
                ModelRevision = 0,
                CorpusRef = "corpus1",
                TextId = "textId",
                SourceRefs = null,
                TargetRefs = null,
                Translation = "translation",
                SourceTokens = null,
                TranslationTokens = null,
                Alignment = null,
                Confidence = null,
                AlignmentScore = null,
                DeletedAt = DateTime.UtcNow.AddSeconds(-1),
            }
        );
        env.Pretranslations.Add(
            new Pretranslation
            {
                Id = "pretranslation2",
                Revision = 0,
                EngineRef = "engine1",
                ModelRevision = 0,
                CorpusRef = "corpus1",
                TextId = "textId",
                SourceRefs = null,
                TargetRefs = null,
                Translation = "translation",
                SourceTokens = null,
                TranslationTokens = null,
                Alignment = null,
                Confidence = null,
                AlignmentScore = null,
                DeletedAt = DateTime.UtcNow.AddSeconds(30),
            }
        );

        var cts = new CancellationTokenSource();
        await env.Service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        await env.Service.StopAsync(cts.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(env.Pretranslations.Contains("pretranslation1"), Is.False);
            Assert.That(env.Pretranslations.Contains("pretranslation2"), Is.True);
        }
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            IOptionsMonitor<PretranslationOptions> options = Substitute.For<IOptionsMonitor<PretranslationOptions>>();
            options.CurrentValue.Returns(
                new PretranslationOptions
                {
                    DeletedPretranslationTimeout = TimeSpan.FromSeconds(1),
                    DeletedPretranslationCleanerSchedule = "* * * * * *",
                }
            );
            Pretranslations = new MemoryRepository<Pretranslation>();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<IRepository<Pretranslation>>(_ => Pretranslations);
            Service = new DeletedPretranslationCleaner(
                serviceCollection.BuildServiceProvider(),
                options,
                Substitute.For<ILogger<DeletedPretranslationCleaner>>()
            );
        }

        public MemoryRepository<Pretranslation> Pretranslations { get; }
        public DeletedPretranslationCleaner Service { get; }
    }
}
