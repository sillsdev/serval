namespace Serval.E2ETests;

[TestFixture]
[Category("E2EStress")]
public class ServalApiStressTests
{
    private ServalClientHelper _helperClient;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSSLErrors: true);
        await _helperClient.InitAsync();
    }

    [SetUp]
    public void Setup()
    {
        _helperClient.Setup();
    }

    [Test]
    public async Task SMTFullBibleStress()
    {
        for (int i = 0; i < 100; i++)
        {
            string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", $"SMT{i}_Stress");
            TranslationEngine engine = await _helperClient.TranslationEnginesClient.GetAsync(engineId);
            Assert.That(engine.IsModelPersisted, Is.True);
            string[] books = ["bible_LARGEFILE.txt"];
            await _helperClient.AddTextCorpusToEngineAsync(engineId, books, "es", "en", false);
            await _helperClient.BuildEngineAsync(engineId);
            TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(
                engineId,
                "Hello World"
            );
            Assert.That(tResult.Translation, Is.Not.Empty);
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        await _helperClient.TearDown();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _helperClient.DisposeAsync();
    }
}
