namespace Serval.ApiServerE2E;

[TestFixture]
[Category("Integration")]
public class E2ETests
{
    private ServalClientHelper _helperClient;

    public E2ETests()
    {
        _helperClient = InitializeClient();
    }

    private ServalClientHelper InitializeClient()
    {
        var clientId = Environment.GetEnvironmentVariable("SERVAL_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("SERVAL_CLIENT_SECRET");
        if (clientId == null)
        {
            Console.WriteLine(
                "You need an auth0 client_id in the environment variable SERVAL_CLIENT_ID!  Look at README for instructions on getting one."
            );
        }
        else if (clientSecret == null)
        {
            Console.WriteLine(
                "You need an auth0 client_secret in the environment variable SERVAL_CLIENT_SECRET!  Look at README for instructions on getting one."
            );
        }
        return new ServalClientHelper(
            "http://machine-api.org/",
            "https://sil-appbuilder.auth0.com",
            "https://machine.sil.org",
            clientId,
            clientSecret,
            ignoreSSLErrors: true
        );
    }

    [Test]
    public async Task GetEchoSuggestion()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "en");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "Espíritu");
    }

    [Test]
    public async Task GetSmtTranslation()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "spirit");
    }

    [Test]
    [Category("slow")]
    public async Task GetSmtWholeBible()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en");
        await _helperClient.PostTextCorpusToEngine(engineId, new string[] { "bible.txt" }, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "Spirit");
    }

    [Test]
    public async Task GetSmtAddSegment()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.AreEqual(tResult.Translation, "ungidos spirit");
        await _helperClient.translationEnginesClient.TrainSegmentAsync(
            engineId,
            new SegmentPair
            {
                SourceSegment = "ungidos espíritu",
                TargetSegment = "unction spirit",
                SentenceStart = true
            }
        );
        TranslationResult tResult2 = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "ungidos espíritu"
        );
        Assert.AreEqual(tResult2.Translation, "unction spirit");
    }

    [Test]
    public async Task GetSmtMoreCorpus()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en");
        await _helperClient.PostTextCorpusToEngine(engineId, new string[] { "3JN.txt" }, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.AreEqual(tResult.Translation, "truth mundo");
        await _helperClient.PostTextCorpusToEngine(engineId, new string[] { "1JN.txt", "2JN.txt" }, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult2 = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.AreEqual(tResult2.Translation, "truth world");
    }

    /*
    @E2E
    Scenario: Get Nmt Pretranslation
        Given a new Nmt engine for John from es to en
        When a text corpora containing MAT.txt, 1JN.txt, 2JN.txt are added to John's engine in es and en
        And a text corpora containing 3JN.txt are added to John's engine in es to translate into en
        And John's engine is built
        Then the pretranslation for John for 3JN.txt starts with "The elder unto the"
    */
}
