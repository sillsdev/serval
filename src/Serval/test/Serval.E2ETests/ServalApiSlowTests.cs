namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
[Category("slow")]
public class ServalApiSlowTests
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
    public async Task GetSmtWholeBible()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT2");
        await _helperClient.AddTextCorpusToEngineAsync(engineId, ["bible.txt"], "es", "en", false);
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("Spirit"));
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
