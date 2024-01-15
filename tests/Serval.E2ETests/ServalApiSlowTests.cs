namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
[Category("slow")]
public class ServalApiSlowTests
{
    private ServalClientHelper _helperClient;

    [SetUp]
    public void SetUp()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSSLErrors: true);
    }

    [Test]
    public async Task GetSmtWholeBible()
    {
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT2");
        await _helperClient.AddTextCorpusToEngine(engineId, ["bible.txt"], "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("Spirit"));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _helperClient.DisposeAsync();
    }
}
