namespace Serval.ApiServerE2E;

[TestFixture]
[Category("E2E")]
public class E2ETests
{
    private ServalClientHelper _helperClient;

    public E2ETests()
    {
        _helperClient = InitializeClient();
    }

    private ServalClientHelper InitializeClient()
    {
        var hostUrl = Environment.GetEnvironmentVariable("SERVAL_HOST_URL");
        var clientId = Environment.GetEnvironmentVariable("SERVAL_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("SERVAL_CLIENT_SECRET");
        var authUrl = Environment.GetEnvironmentVariable("SERVAL_AUTH_URL");
        if (hostUrl == null)
        {
            Console.WriteLine(
                "You need a serval host url in the environment variable SERVAL_HOST_URL!  Look at README for instructions on getting one."
            );
        }
        else if (clientId == null)
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
        else if (authUrl == null)
        {
            Console.WriteLine(
                "You need an auth0 authorization url in the environment variable SERVAL_AUTH_URL!  Look at README for instructions on getting one."
            );
        }

        return new ServalClientHelper(
            hostUrl,
            authUrl,
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
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "en", "Echo1");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "Espíritu");
    }

    [Test]
    public async Task GetSmtTranslation()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT1");
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
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT2");
        await _helperClient.PostTextCorpusToEngine(engineId, new string[] { "bible.txt" }, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "Spirit");
    }

    [Test]
    public async Task GetSmtAddSegment()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT3");
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
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT4");
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

    [Test]
    public async Task NmtBatch()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT1");
        var books = new string[] { "MAT.txt", "1JN.txt", "2JN.txt" };
        await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
        var cId = await _helperClient.PostTextCorpusToEngine(engineId, new string[] { "3JN.txt" }, "es", "en", true);
        await _helperClient.BuildEngine(engineId);
        IList<Pretranslation> lTrans = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId
        );
        Assert.IsTrue(lTrans[0].Translation.Contains("dearly beloved Gaius"));
    }
}
