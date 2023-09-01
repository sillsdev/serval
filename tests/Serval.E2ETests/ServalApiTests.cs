namespace Serval.E2ETests;

[TestFixture]
[Category("E2E")]
public class ServalApiTests
{
    private ServalClientHelper _helperClient;

    public ServalApiTests()
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
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "es", "Echo1");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "Espíritu");
    }

    [Test]
    public async Task GetEchoPretranslate()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Echo", "es", "es", "Echo2");
        var books = new string[] { "1JN.txt", "2JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", false);
        books = new string[] { "3JN.txt" };
        var corpusId = await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "es", true);
        await _helperClient.BuildEngine(engineId);
        var corpora = _helperClient.translationEnginesClient.GetAllCorporaAsync(engineId);
        var pretranslations = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            corpusId
        );
        Assert.IsTrue(pretranslations.Count > 1);
    }

    [Test]
    public async Task GetSmtTranslation()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT1");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
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
        await _helperClient.AddTextCorpusToEngine(engineId, new string[] { "bible.txt" }, "es", "en", false);
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
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
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
        await _helperClient.AddTextCorpusToEngine(engineId, new string[] { "3JN.txt" }, "es", "en", false);
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(
            engineId,
            "verdad mundo"
        );
        Assert.AreEqual(tResult.Translation, "truth mundo");
        await _helperClient.AddTextCorpusToEngine(engineId, new string[] { "1JN.txt", "2JN.txt" }, "es", "en", false);
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
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        var cId = await _helperClient.AddTextCorpusToEngine(engineId, new string[] { "3JN.txt" }, "es", "en", true);
        await _helperClient.BuildEngine(engineId);
        IList<Pretranslation> lTrans = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            cId
        );
        Assert.IsTrue(lTrans[0].Translation.Contains("dearly beloved Gaius"));
    }

    [Test]
    public async Task GetNmtCancelAndRestartBuild()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("Nmt", "es", "en", "NMT2");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        // start and cancel first job after 2 seconds
        var build = await _helperClient.StartBuildAsync(engineId);
        await Task.Delay(4000);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        await _helperClient.translationEnginesClient.CancelBuildAsync(engineId);
        await Task.Delay(2000);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Canceled);

        // do a second job normally and make sure it works.
        build = await _helperClient.StartBuildAsync(engineId);
        await Task.Delay(4000);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Active || build.State == JobState.Pending);

        // and cancel again - let's not wait forever
        await _helperClient.translationEnginesClient.CancelBuildAsync(engineId);
        await Task.Delay(2000);
        build = await _helperClient.translationEnginesClient.GetBuildAsync(engineId, build.Id);
        Assert.That(build.State == JobState.Canceled);
    }

    [Test]
    public async Task CircuitousRouteGetWordGraphAsync()
    {
        await _helperClient.ClearEngines();

        //Create smt engine
        string smtEngineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT5");

        //Try to get word graph - should fail: unbuilt
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await _helperClient.translationEnginesClient.GetWordGraphAsync(smtEngineId, "verdad");
        });
        Assert.NotNull(ex);
        Assert.That(ex!.StatusCode, Is.EqualTo(409));

        //Add corpus
        var cId = await _helperClient.AddTextCorpusToEngine(
            smtEngineId,
            new string[] { "2JN.txt", "3JN.txt" },
            "es",
            "en",
            false
        );

        //Build the new engine
        await _helperClient.BuildEngine(smtEngineId);

        //Remove added corpus (shouldn't affect translation)
        await _helperClient.translationEnginesClient.DeleteCorpusAsync(smtEngineId, cId);

        //Add corpus
        await _helperClient.AddTextCorpusToEngine(
            smtEngineId,
            new string[] { "1JN.txt", "2JN.txt", "3JN.txt" },
            "es",
            "en",
            false
        );

        //Build the new engine
        await _helperClient.BuildEngine(smtEngineId);

        WordGraph result = await _helperClient.translationEnginesClient.GetWordGraphAsync(smtEngineId, "verdad");
        Assert.That(result.SourceTokens, Has.Count.EqualTo(1));
        Assert.That(
            result.Arcs
                .Where(arc => arc != null && arc.Confidences != null)!
                .MaxBy(arc => arc.Confidences.Average())!
                .TargetTokens.All(tk => tk == "truth"),
            "Best translation should have been 'truth'but returned word graph: \n{0}",
            JsonSerializer.Serialize(result)
        );
    }

    [Test]
    public async Task CircuitousRouteTranslateTopNAsync()
    {
        const int N = 3;

        //Create engine
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "en", "fa", "SMT6");

        //Retrieve engine
        TranslationEngine? engine = await _helperClient.translationEnginesClient.GetAsync(engineId);
        Assert.NotNull(engine);
        Assert.That(engine.Type, Is.EqualTo("SmtTransfer"));

        //Add corpus
        string cId = await _helperClient.AddTextCorpusToEngine(
            engineId,
            new string[] { "1JN.txt", "2JN.txt", "3JN.txt" },
            "en",
            "fa",
            false
        );

        //Retrieve corpus
        TranslationCorpus? corpus = await _helperClient.translationEnginesClient.GetCorpusAsync(engineId, cId);
        Assert.NotNull(corpus);
        Assert.That(corpus.SourceLanguage, Is.EqualTo("en"));
        Assert.That(corpus.TargetFiles, Has.Count.EqualTo(3));

        //Build engine
        await _helperClient.BuildEngine(engineId);

        //Get top `N` translations
        ICollection<TranslationResult> results = await _helperClient.translationEnginesClient.TranslateNAsync(
            engineId,
            N,
            "love"
        );
        Assert.NotNull(results);
        Assert.That(
            results.MaxBy(t => t.Confidences.Average())!.Translation,
            Is.EqualTo("amour"),
            "Expected best translation to be 'amour' but results were this:\n" + JsonSerializer.Serialize(results)
        );
    }

    [Test]
    public async Task GetSmtCancelAndRestartBuild()
    {
        await _helperClient.ClearEngines();
        string engineId = await _helperClient.CreateNewEngine("SmtTransfer", "es", "en", "SMT7");
        var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
        await _helperClient.AddTextCorpusToEngine(engineId, books, "es", "en", false);
        // start and cancel first job after 2 seconds
        await _helperClient.StartBuildAsync(engineId);
        await Task.Delay(2000);
        await _helperClient.translationEnginesClient.CancelBuildAsync(engineId);
        await Task.Delay(2000);
        // do a second job normally and make sure it works.
        await _helperClient.BuildEngine(engineId);
        TranslationResult tResult = await _helperClient.translationEnginesClient.TranslateAsync(engineId, "Espíritu");
        Assert.AreEqual(tResult.Translation, "spirit");
    }
}
