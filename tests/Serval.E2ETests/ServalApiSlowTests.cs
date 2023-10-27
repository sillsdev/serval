namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
[Category("slow")]
public class ServalApiSlowTests
{
    private ServalClientHelper? _helperClient;

    [SetUp]
    public void SetUp()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSSLErrors: true);
    }

    [Test]
    public async Task GetSmtWholeBible()
    {
        await _helperClient!.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT2");
        await _helperClient.AddTextCorpusToEngine(engineId, new string[] { "bible.txt" }, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "Spirit");
    }
}
