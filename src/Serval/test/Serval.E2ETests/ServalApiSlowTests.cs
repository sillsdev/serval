namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
[Category("slow")]
[Explicit("These are only manually run occasionally due to their speed")]
public class ServalApiSlowTests
{
    private ServalClientHelper _helperClient;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _helperClient = new ServalClientHelper("https://serval-api.org/", ignoreSslErrors: true);
        await _helperClient.InitAsync();
    }

    [SetUp]
    public void Setup()
    {
        _helperClient.Setup();
    }

    [Test]
    [Obsolete("Legacy corpora are deprecated")]
    public async Task GetSmtWholeBible_LegacyCorpus()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT2");
        await _helperClient.AddLegacyCorpusToEngineAsync(engineId, ["bible.txt"], "es", "en", false);
        await _helperClient.BuildEngineAsync(engineId);
        TranslationResult tResult = await _helperClient.TranslationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.That(tResult.Translation, Is.EqualTo("Spirit"));
    }

    [Test]
    public async Task GetSmtWholeBible_ParallelCorpus()
    {
        string engineId = await _helperClient.CreateNewEngineAsync("SmtTransfer", "es", "en", "SMT2");
        ParallelCorpusConfig trainCorpus = await _helperClient.MakeParallelTextCorpus(["bible.txt"], "es", "en", false);
        await _helperClient.AddParallelTextCorpusToEngineAsync(engineId, trainCorpus, false);
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
